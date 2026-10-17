using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.Imports;

internal sealed record XmlArticle(XElement Element, string? Currency);

/// <summary>
/// The wholesaler's XML export (Portuguese element names). Format specifics: streamed element by element
/// (the file can be large), currency declared once on the root, packaging as wholesaler unit codes, and
/// weight in grams.
/// <code>
/// &lt;catalogo moeda="ZAR"&gt;
///   &lt;artigo codigo="LEB-001"&gt;&lt;descricao/&gt;&lt;categoria/&gt;&lt;unidade&gt;CX12&lt;/unidade&gt;
///     &lt;preco&gt;215.00&lt;/preco&gt;&lt;stock/&gt;&lt;peso_gramas/&gt;&lt;iva/&gt;&lt;/artigo&gt;
/// &lt;/catalogo&gt;
/// </code>
/// </summary>
internal sealed class XmlCatalogImporter(ICatalogBatchWriter writer, ITaxRateTable taxRates)
    : CatalogImporter<XmlArticle>(writer, taxRates)
{
    private static readonly string[] RequiredElements = ["descricao", "categoria", "unidade", "preco", "stock", "peso_gramas", "iva"];

    public override ImportFormat Format => ImportFormat.Xml;

    protected override async IAsyncEnumerable<SourceRecord<XmlArticle>> ReadAsync(
        ImportSource source, ImportReportBuilder report, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true };
        using var reader = XmlReader.Create(source.Content!, settings);

        string? currency = null;
        while (true)
        {
            (XElement? Element, int Line) next;
            try
            {
                if (currency is null && await reader.MoveToContentAsync() == XmlNodeType.Element)
                {
                    if (reader.LocalName != "catalogo")
                    {
                        report.Fail("Root element must be <catalogo>.");
                        yield break;
                    }

                    currency = reader.GetAttribute("moeda") ?? Currency.Mzn.Code;
                }

                next = await NextArticleAsync(reader, cancellationToken);
            }
            catch (XmlException exception)
            {
                report.Fail($"Malformed XML at line {exception.LineNumber}: {exception.Message}");
                yield break;
            }

            if (next.Element is null)
            {
                yield break;
            }

            yield return new SourceRecord<XmlArticle>(next.Line, next.Element.Attribute("codigo")?.Value, new XmlArticle(next.Element, currency));
        }
    }

    protected override IEnumerable<string> ValidateRecord(XmlArticle record)
    {
        if (string.IsNullOrWhiteSpace(record.Element.Attribute("codigo")?.Value))
        {
            yield return "Attribute 'codigo' is required on <artigo>.";
        }

        foreach (var name in RequiredElements.Where(n => record.Element.Element(n) is null))
        {
            yield return $"Element <{name}> is missing.";
        }
    }

    protected override Result<ProductDetails> Map(XmlArticle record)
    {
        var article = record.Element;
        var errors = new List<FieldError>();

        var variant = WholesalerUnitCodes.Parse(article.Element("unidade")?.Value, VariantAttributesPool.Shared);
        var currency = Currency.FromCode(record.Currency);
        var taxCategory = TaxCategory.From(article.Element("iva")?.Value);

        if (!ImportValues.TryParseDecimal(article.Element("preco")?.Value, out var price))
        {
            errors.Add(new FieldError("preco", "Price is not a number."));
        }

        if (!ImportValues.TryParseInt(article.Element("stock")?.Value, out var stock))
        {
            errors.Add(new FieldError("stock", "Stock is not a whole number."));
        }

        if (!ImportValues.TryParseInt(article.Element("peso_gramas")?.Value, out var grams))
        {
            errors.Add(new FieldError("peso_gramas", "Weight is not a whole number of grams."));
        }

        foreach (var result in new Result[] { variant, currency, taxCategory }.Where(r => r.IsFailure))
        {
            errors.AddRange(result.Error is ValidationError v ? v.Errors : [new FieldError("artigo", result.Error.Message)]);
        }

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        return new ProductDetails(
            article.Attribute("codigo")!.Value.Trim(),
            article.Element("descricao")!.Value.Trim(),
            article.Element("categoria")!.Value.Trim(),
            variant.Value,
            Money.Of(price, currency.Value),
            stock,
            grams / 1000m,
            taxCategory.Value);
    }

    private static async Task<(XElement? Element, int Line)> NextArticleAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        while (!reader.EOF)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "artigo")
            {
                var line = (reader as IXmlLineInfo)?.LineNumber ?? 0;
                var element = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                return (element, line);
            }

            await reader.ReadAsync();
        }

        return (null, 0);
    }
}

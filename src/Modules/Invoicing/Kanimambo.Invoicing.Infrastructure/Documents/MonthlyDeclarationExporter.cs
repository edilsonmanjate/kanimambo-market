using System.Globalization;
using System.Text;
using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>Picks the visitor by format from a table (no switch) and borrows pooled buffers for the text formats.</summary>
internal sealed class MonthlyDeclarationExporter : IMonthlyDeclarationExporter
{
    private readonly Dictionary<string, Func<FiscalPeriod, ExportedFile>> _formats;
    private readonly InvoicingPools _pools;

    public MonthlyDeclarationExporter(InvoicingPools pools)
    {
        _pools = pools;
        _formats = new(StringComparer.OrdinalIgnoreCase)
        {
            ["csv"] = p => Text(p, sb => new EDeclaracaoCsvVisitor(sb), "csv", "text/csv"),
            ["xml"] = p => Text(p, sb => new EDeclaracaoXmlVisitor(sb), "xml", "application/xml"),
            ["xlsx"] = Xlsx,
        };
    }

    public IReadOnlyCollection<string> Formats => _formats.Keys;

    public Result<ExportedFile> Export(FiscalPeriod period, string format) =>
        _formats.TryGetValue(format, out var export)
            ? export(period)
            : Error.Validation(new FieldError("format", $"Unknown format '{format}'. Use one of: {string.Join(", ", _formats.Keys)}."));

    private static string FileName(FiscalPeriod p, string extension) =>
        string.Create(CultureInfo.InvariantCulture, $"e-declaracao-{p.IssuerNuit}-{p.Year}-{p.Month:D2}.{extension}");

    private ExportedFile Text(FiscalPeriod period, Func<StringBuilder, IFiscalDocumentVisitor> visitor, string extension, string contentType)
    {
        var buffer = _pools.StringBuilders.Get();
        try
        {
            period.Accept(visitor(buffer));
            return new ExportedFile(FileName(period, extension), contentType, Encoding.UTF8.GetBytes(buffer.ToString()));
        }
        finally
        {
            _pools.StringBuilders.Return(buffer);
        }
    }

    private static ExportedFile Xlsx(FiscalPeriod period)
    {
        using var visitor = new EDeclaracaoXlsxVisitor();
        period.Accept(visitor);
        return new ExportedFile(FileName(period, "xlsx"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", visitor.ToArray());
    }
}

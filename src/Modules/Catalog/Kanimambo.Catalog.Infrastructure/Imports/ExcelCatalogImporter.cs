using System.Globalization;
using ClosedXML.Excel;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.Imports;

internal sealed record ExcelRow(IReadOnlyDictionary<string, XLCellValue> Cells);

/// <summary>
/// Excel workbook (.xlsx) filled in by the seller. Format specifics: first worksheet, header in row 1,
/// numbers arrive as typed numeric cells — text typed into a numeric column is rejected with a clear reason.
/// </summary>
internal sealed class ExcelCatalogImporter(ICatalogBatchWriter writer, ITaxRateTable taxRates)
    : CatalogImporter<ExcelRow>(writer, taxRates)
{
    private static readonly string[] NumericColumns = [TabularColumns.PackSize, TabularColumns.Price, TabularColumns.Stock, TabularColumns.WeightKg];

    public override ImportFormat Format => ImportFormat.Excel;

    protected override IAsyncEnumerable<SourceRecord<ExcelRow>> ReadAsync(
        ImportSource source, ImportReportBuilder report, CancellationToken cancellationToken) =>
        ReadRows(source, report).ToAsyncEnumerable();

    protected override IEnumerable<string> ValidateRecord(ExcelRow record)
    {
        foreach (var column in NumericColumns)
        {
            if (record.Cells.TryGetValue(column, out var value) && !value.IsNumber && !value.IsBlank)
            {
                yield return $"Column '{column}' must be a numeric cell (found text '{value}').";
            }
        }
    }

    protected override Result<ProductDetails> Map(ExcelRow record) =>
        TabularColumns.ToDetails(
            column => record.Cells.TryGetValue(column, out var value) && !value.IsBlank
                ? Convert.ToString(value.IsNumber ? value.GetNumber() : (object)value.ToString(), CultureInfo.InvariantCulture)
                : null,
            column => record.Cells.TryGetValue(column, out var value) && value.IsNumber
                ? (true, (decimal)value.GetNumber())
                : (false, 0m));

    private static IEnumerable<SourceRecord<ExcelRow>> ReadRows(ImportSource source, ImportReportBuilder report)
    {
        using var workbook = Open(source.Content!, report);
        if (workbook is null)
        {
            yield break;
        }

        var sheet = workbook.Worksheets.First();
        var header = sheet.Row(1).CellsUsed()
            .ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber);

        var missing = TabularColumns.Required.Except(header.Keys).ToArray();
        if (missing.Length > 0)
        {
            report.Fail(TabularColumns.Describe(missing));
            yield break;
        }

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > 1))
        {
            var cells = header.ToDictionary(h => h.Key, h => row.Cell(h.Value).Value, StringComparer.Ordinal);
            var sku = cells[TabularColumns.Sku].IsBlank ? null : cells[TabularColumns.Sku].ToString();
            yield return new SourceRecord<ExcelRow>(row.RowNumber(), sku, new ExcelRow(cells));
        }
    }

    private static XLWorkbook? Open(Stream content, ImportReportBuilder report)
    {
        try
        {
            return new XLWorkbook(content);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or FormatException)
        {
            report.Fail($"Not a valid .xlsx workbook ({exception.GetType().Name}).");
            return null;
        }
    }
}

using System.Runtime.CompilerServices;
using System.Text;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.Imports;

internal sealed record CsvRow(IReadOnlyDictionary<string, string> Fields, int FieldCount, int ExpectedFieldCount);

/// <summary>
/// CSV exported from the seller's spreadsheet. Format specifics: <c>;</c> or <c>,</c> separator detected
/// from the header, quoted fields, and Portuguese decimals (<c>1.250,50</c>).
/// </summary>
internal sealed class CsvCatalogImporter(ICatalogBatchWriter writer, ITaxRateTable taxRates)
    : CatalogImporter<CsvRow>(writer, taxRates)
{
    public override ImportFormat Format => ImportFormat.Csv;

    protected override async IAsyncEnumerable<SourceRecord<CsvRow>> ReadAsync(
        ImportSource source, ImportReportBuilder report, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(source.Content!, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            report.Fail("The file is empty.");
            yield break;
        }

        var separator = headerLine.Count(c => c == ';') >= headerLine.Count(c => c == ',') ? ';' : ',';
        var header = Split(headerLine, separator).Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var missing = TabularColumns.Required.Except(header).ToArray();
        if (missing.Length > 0)
        {
            report.Fail(TabularColumns.Describe(missing));
            yield break;
        }

        var lineNumber = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = Split(line, separator);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < Math.Min(header.Length, values.Count); i++)
            {
                fields[header[i]] = values[i].Trim();
            }

            yield return new SourceRecord<CsvRow>(lineNumber, fields.GetValueOrDefault(TabularColumns.Sku), new CsvRow(fields, values.Count, header.Length));
        }
    }

    protected override IEnumerable<string> ValidateRecord(CsvRow record)
    {
        if (record.FieldCount != record.ExpectedFieldCount)
        {
            yield return $"Expected {record.ExpectedFieldCount} columns but found {record.FieldCount} (check the separator and quotes).";
        }
    }

    protected override Result<ProductDetails> Map(CsvRow record) =>
        TabularColumns.ToDetails(
            column => record.Fields.GetValueOrDefault(column),
            column => ImportValues.TryParseDecimal(record.Fields.GetValueOrDefault(column), out var value) ? (true, value) : (false, 0m));

    /// <summary>Minimal RFC 4180 field splitter (quotes, escaped quotes) for one line.</summary>
    internal static List<string> Split(string line, char separator)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == separator)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}

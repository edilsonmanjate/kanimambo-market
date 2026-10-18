namespace Kanimambo.Catalog.Application.Imports;

/// <summary>Collects the outcome of each step; the Report step turns it into an <see cref="ImportReport"/>.</summary>
public sealed class ImportReportBuilder(ImportFormat format, string source)
{
    private readonly List<RejectedRow> _rejected = [];

    public int TotalRows { get; private set; }

    public int Created { get; private set; }

    public int Updated { get; private set; }

    public string? FatalError { get; private set; }

    public void CountRow() => TotalRows++;

    public void Reject(int line, string? reference, IReadOnlyList<string> reasons) => _rejected.Add(new RejectedRow(line, reference, reasons));

    public void Fail(string reason) => FatalError ??= reason;

    internal void Add(BatchWriteResult result)
    {
        Created += result.Created;
        Updated += result.Updated;
        _rejected.AddRange(result.Rejected);
    }

    public ImportReport Build() =>
        new(format, source, TotalRows, Created, Updated, [.. _rejected.OrderBy(r => r.Line)], FatalError);
}

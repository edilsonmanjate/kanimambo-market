using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;

namespace Kanimambo.Catalog.Application.Imports;

public enum ImportFormat
{
    Csv,
    Excel,
    Xml,
    WholesalerApi,
}

/// <summary>Where the rows come from: an uploaded file (<see cref="Content"/>) or a remote API (no content).</summary>
public sealed record ImportSource(string Name, Stream? Content);

/// <summary>One raw record as read from the source, with its position for the report.</summary>
public sealed record SourceRecord<TRecord>(int Line, string? Reference, TRecord Value);

/// <summary>A row that passed validation and mapping, ready to be persisted.</summary>
public sealed record ProductDraft(int Line, ProductDetails Details);

public sealed record RejectedRow(int Line, string? Reference, IReadOnlyList<string> Reasons);

public sealed record BatchWriteResult(int Created, int Updated, IReadOnlyList<RejectedRow> Rejected);

public sealed record ImportReport(
    ImportFormat Format,
    string Source,
    int TotalRows,
    int Created,
    int Updated,
    IReadOnlyList<RejectedRow> Rejected,
    string? FatalError)
{
    public int Accepted => Created + Updated;

    public bool Succeeded => FatalError is null;
}

/// <summary>Persist step: upserts a batch of drafts by (seller, SKU) and releases tracked entities.</summary>
public interface ICatalogBatchWriter
{
    Task<BatchWriteResult> WriteAsync(SellerId sellerId, IReadOnlyList<ProductDraft> drafts, CancellationToken cancellationToken);
}

public interface ICatalogImporter
{
    ImportFormat Format { get; }

    Task<ImportReport> ImportAsync(SellerId sellerId, ImportSource source, CancellationToken cancellationToken);
}

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

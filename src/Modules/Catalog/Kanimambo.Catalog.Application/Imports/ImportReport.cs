namespace Kanimambo.Catalog.Application.Imports;

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

namespace Kanimambo.Catalog.Application.Imports;

public sealed record BatchWriteResult(int Created, int Updated, IReadOnlyList<RejectedRow> Rejected);

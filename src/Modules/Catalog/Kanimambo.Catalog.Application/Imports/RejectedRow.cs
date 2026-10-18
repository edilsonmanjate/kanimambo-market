namespace Kanimambo.Catalog.Application.Imports;

public sealed record RejectedRow(int Line, string? Reference, IReadOnlyList<string> Reasons);

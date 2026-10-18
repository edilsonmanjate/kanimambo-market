namespace Kanimambo.Catalog.Application.Imports;

/// <summary>One raw record as read from the source, with its position for the report.</summary>
public sealed record SourceRecord<TRecord>(int Line, string? Reference, TRecord Value);

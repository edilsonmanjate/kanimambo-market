namespace Kanimambo.Catalog.Infrastructure.Imports;

internal sealed record CsvRow(IReadOnlyDictionary<string, string> Fields, int FieldCount, int ExpectedFieldCount);

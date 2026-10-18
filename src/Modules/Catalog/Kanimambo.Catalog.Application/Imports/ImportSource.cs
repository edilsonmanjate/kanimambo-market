namespace Kanimambo.Catalog.Application.Imports;

/// <summary>Where the rows come from: an uploaded file (<see cref="Content"/>) or a remote API (no content).</summary>
public sealed record ImportSource(string Name, Stream? Content);

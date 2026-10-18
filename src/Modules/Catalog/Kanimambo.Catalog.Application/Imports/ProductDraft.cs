using Kanimambo.Catalog.Domain.Products;

namespace Kanimambo.Catalog.Application.Imports;

/// <summary>A row that passed validation and mapping, ready to be persisted.</summary>
public sealed record ProductDraft(int Line, ProductDetails Details);

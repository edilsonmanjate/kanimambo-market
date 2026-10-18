namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record PriceChange(Guid ProductId, string Sku, decimal OldAmount, decimal NewAmount, string Currency);

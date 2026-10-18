using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Application.Acceptance;

public sealed record AcceptanceLine(Guid ProductId, Guid SellerId, string Sku, int Quantity, int AvailableStock, Money LineTotal);

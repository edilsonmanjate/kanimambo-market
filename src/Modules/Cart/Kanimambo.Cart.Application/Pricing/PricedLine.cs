using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Cart.Application.Pricing;

public sealed record PricedLine(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    Money UnitPrice,
    IExchangeRate Rate,
    Money LineTotalMzn,
    LineAvailability Availability);

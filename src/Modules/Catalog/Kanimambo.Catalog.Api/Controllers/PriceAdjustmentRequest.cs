namespace Kanimambo.Catalog.Api.Controllers;

public sealed record PriceAdjustmentRequest(decimal Percentage, string Reason);

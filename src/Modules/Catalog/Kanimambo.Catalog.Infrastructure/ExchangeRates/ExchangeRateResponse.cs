namespace Kanimambo.Catalog.Infrastructure.ExchangeRates;

internal sealed record ExchangeRateResponse(string Base, string Quote, DateOnly Date, decimal Rate);

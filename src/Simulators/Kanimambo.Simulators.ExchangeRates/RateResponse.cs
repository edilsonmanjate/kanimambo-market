namespace Kanimambo.Simulators.ExchangeRates;

public sealed record RateResponse(string Base, string Quote, DateOnly Date, decimal Rate, string Source);

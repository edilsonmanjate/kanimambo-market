namespace Kanimambo.Catalog.Infrastructure.ExchangeRates;

public sealed class ExchangeRatesOptions
{
    public const string SectionName = "Catalog:ExchangeRates";

    public Uri BaseAddress { get; set; } = new("https+http://exchange-rates");

    /// <summary>Upstream quota the proxy enforces (the rates API rejects bursts with 429).</summary>
    public int MaxUpstreamCallsPerMinute { get; set; } = 20;
}

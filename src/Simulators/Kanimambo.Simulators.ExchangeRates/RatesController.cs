using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kanimambo.Simulators.ExchangeRates;

public sealed record RateResponse(string Base, string Quote, DateOnly Date, decimal Rate, string Source);

[ApiController]
[Route("api/rates")]
public sealed class RatesController(SimulatorOptions options, TimeProvider timeProvider) : ControllerBase
{
    private const string Mzn = "MZN";

    [HttpGet("{baseCurrency}/{quoteCurrency}")]
    [EnableRateLimiting(ExchangeRatesSimulator.RatePolicy)]
    public async Task<IActionResult> Get(string baseCurrency, string quoteCurrency, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(options.LatencyMilliseconds), timeProvider, cancellationToken);

        if (!string.Equals(quoteCurrency, Mzn, StringComparison.OrdinalIgnoreCase)
            || !options.BaseRatesToMzn.TryGetValue(baseCurrency, out var reference))
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"No rate for {baseCurrency}/{quoteCurrency}.");
        }

        var day = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Deterministic daily drift in [-0.5 %, +0.5 %], so the same date always returns the same rate.
        var drift = (((long)day.DayNumber * 7919 % 1001) - 500) / 100_000m;
        var rate = decimal.Round(reference * (1m + drift), 4, MidpointRounding.AwayFromZero);

        return Ok(new RateResponse(baseCurrency.ToUpperInvariant(), Mzn, day, rate, "Kanimambo exchange-rate simulator (not official rates)"));
    }
}

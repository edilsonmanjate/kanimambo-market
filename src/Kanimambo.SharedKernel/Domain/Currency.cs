using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Domain;

/// <summary>
/// ISO 4217 currency. MZN is the settlement currency; USD and ZAR appear as base prices
/// of importing sellers and are converted at the daily exchange rate.
/// </summary>
public sealed record Currency
{
    public static readonly Currency Mzn = new("MZN");
    public static readonly Currency Usd = new("USD");
    public static readonly Currency Zar = new("ZAR");

    private static readonly Dictionary<string, Currency> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        [Mzn.Code] = Mzn,
        [Usd.Code] = Usd,
        [Zar.Code] = Zar,
    };

    private Currency(string code) => Code = code;

    public string Code { get; }

    public static Result<Currency> FromCode(string? code) =>
        code is not null && Known.TryGetValue(code.Trim(), out var currency)
            ? currency
            : Error.Validation(new FieldError("currency", $"Unsupported currency '{code}'."));

    public override string ToString() => Code;
}

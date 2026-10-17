using System.Globalization;
using System.Text.RegularExpressions;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.Imports;

internal static class ImportValues
{
    /// <summary>
    /// Accepts the decimal styles sellers actually send: <c>1250.50</c>, <c>1250,50</c> and the
    /// Portuguese <c>1.250,50</c> / <c>1 250,50</c> (thousand separators).
    /// </summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains(',', StringComparison.Ordinal))
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }

        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseInt(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
}

/// <summary>
/// The wholesaler encodes packaging as compact unit codes: <c>SC25KG</c> = saco 25 kg, <c>CX12</c> = caixa
/// 12 un, <c>BD5L</c> = bidão 5 l, <c>UN</c> = unidade. Parsed into shared flyweight attributes.
/// </summary>
internal static partial class WholesalerUnitCodes
{
    private static readonly Dictionary<string, string> Packaging = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SC"] = "saco",
        ["CX"] = "caixa",
        ["BD"] = "bidão",
        ["GR"] = "garrafa",
        ["PC"] = "pacote",
        ["UN"] = "unidade",
    };

    public static Result<VariantAttributes> Parse(string? code, VariantAttributesPool pool)
    {
        var match = code is null ? null : UnitCodePattern().Match(code.Trim());
        if (match is not { Success: true } || !Packaging.TryGetValue(match.Groups["pack"].Value, out var packaging))
        {
            return Error.Validation(new FieldError("unit", $"Unknown wholesaler unit code '{code}'."));
        }

        var size = match.Groups["size"].Success
            ? decimal.Parse(match.Groups["size"].Value.Replace(',', '.'), CultureInfo.InvariantCulture)
            : 1m;
        var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : "un";

        return pool.Get(packaging, size, unit);
    }

    [GeneratedRegex(@"^(?<pack>[A-Za-z]{2})(?<size>\d+(?:[.,]\d+)?)?(?<unit>KG|G|L|ML|UN)?$", RegexOptions.IgnoreCase)]
    private static partial Regex UnitCodePattern();
}

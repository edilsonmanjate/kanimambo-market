using System.Globalization;

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

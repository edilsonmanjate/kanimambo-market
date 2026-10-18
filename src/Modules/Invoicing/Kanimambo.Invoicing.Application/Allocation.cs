namespace Kanimambo.Invoicing.Application;

internal static class Allocation
{
    /// <summary>
    /// Splits <paramref name="total"/> (in MZN, 2 decimals) in proportion to <paramref name="weights"/> so the parts add up
    /// exactly: each part is rounded down to the cent and the leftover cents go to the largest remainders.
    /// Used to spread an order's discount over its lines, and a bundle line over its tax categories.
    /// </summary>
    public static decimal[] Split(decimal total, IReadOnlyList<decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var parts = new decimal[weights.Count];
        var sum = weights.Sum();
        if (weights.Count == 0 || total == 0m)
        {
            return parts;
        }

        if (sum == 0m)
        {
            parts[^1] = total;
            return parts;
        }

        var cents = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero);
        var exact = weights.Select(w => cents * w / sum).ToArray();
        var whole = exact.Select(e => (long)Math.Floor(e)).ToArray();
        var leftover = cents - whole.Sum();
        foreach (var index in exact.Select((e, i) => (Remainder: e - Math.Floor(e), Index: i)).OrderByDescending(x => x.Remainder).ThenBy(x => x.Index).Take((int)leftover))
        {
            whole[index.Index]++;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = whole[i] / 100m;
        }

        return parts;
    }
}

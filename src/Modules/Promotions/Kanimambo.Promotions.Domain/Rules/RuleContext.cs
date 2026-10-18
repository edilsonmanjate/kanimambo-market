namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>The values of every variable for one evaluation (one cart, one moment).</summary>
public sealed class RuleContext(IReadOnlyDictionary<string, RuleValue> values)
{
    public RuleValue Get(string variable) =>
        values.TryGetValue(variable, out var value)
            ? value
            : throw new InvalidOperationException($"No value supplied for rule variable '{variable}'.");
}

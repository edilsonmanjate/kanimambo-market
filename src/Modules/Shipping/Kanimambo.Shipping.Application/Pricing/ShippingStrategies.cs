using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>The registered strategies, found by method name. This lookup replaces the switch over delivery methods.</summary>
public sealed class ShippingStrategies(IEnumerable<IShippingStrategy> strategies)
{
    private readonly Dictionary<string, IShippingStrategy> _byMethod =
        strategies.ToDictionary(s => s.Method, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IShippingStrategy> All => _byMethod.Values;

    public Result<IShippingStrategy> Find(string method) =>
        _byMethod.TryGetValue(method, out var strategy)
            ? Result.Success(strategy)
            : Error.Validation(new FieldError("deliveryMethod", $"Unknown delivery method '{method}'. Use one of: {string.Join(", ", _byMethod.Keys)}."));
}

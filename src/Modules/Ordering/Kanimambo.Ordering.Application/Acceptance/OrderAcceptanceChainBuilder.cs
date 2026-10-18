namespace Kanimambo.Ordering.Application.Acceptance;

/// <summary>Declares the checks and their order; the order is the registration order.</summary>
public sealed class OrderAcceptanceChainBuilder
{
    private readonly List<Type> _steps = [];

    internal IReadOnlyList<Type> Steps => _steps;

    public OrderAcceptanceChainBuilder Then<THandler>()
        where THandler : OrderAcceptanceHandler
    {
        _steps.Add(typeof(THandler));
        return this;
    }
}

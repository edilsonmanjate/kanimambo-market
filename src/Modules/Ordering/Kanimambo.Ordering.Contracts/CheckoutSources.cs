namespace Kanimambo.Ordering.Contracts;

/// <summary>Where an order came from: a shopping cart or a B2B draft (reorder).</summary>
public static class CheckoutSources
{
    public const string Cart = "cart";
    public const string Draft = "draft";
}

using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Cart.Domain;

/// <summary>
/// Null Object for anonymous shoppers (many customers buy without an account). It behaves like a customer —
/// it has a type for promotions and an access rule (whoever holds the cart id) — so cart and pricing code
/// never checks for a missing customer.
/// </summary>
public sealed class GuestCustomer : Customer
{
    public static GuestCustomer Instance { get; } = new();

    private GuestCustomer()
    {
    }

    public override string Id => string.Empty;

    public override string Type => GuestType;

    public override bool CanAccess(ICurrentActor actor) => true;
}

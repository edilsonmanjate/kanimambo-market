using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Cart.Domain;

/// <summary>Who owns a cart. Every cart has one — anonymous shoppers get <see cref="GuestCustomer"/>, never null.</summary>
public abstract class Customer
{
    public const string GuestType = "guest";
    public const string RegisteredType = "registered";
    public const string BusinessType = "b2b";

    /// <summary>Stable identifier (the account name); empty for guests.</summary>
    public abstract string Id { get; }

    /// <summary><c>guest</c>, <c>registered</c> or <c>b2b</c> — the value promotion rules see as <c>customer.type</c>.</summary>
    public abstract string Type { get; }

    public abstract bool CanAccess(ICurrentActor actor);

    public static Customer For(ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.IsAuthenticated ? new RegisteredCustomer(actor.Name, actor.IsInRole(KnownRoles.B2B)) : GuestCustomer.Instance;
    }

    /// <summary>Rebuilds the customer from persisted columns: the one place where "no customer" is translated.</summary>
    internal static Customer Restore(string? id, string? type) =>
        string.IsNullOrEmpty(id) ? GuestCustomer.Instance : new RegisteredCustomer(id, type == BusinessType);
}

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

public sealed class RegisteredCustomer(string id, bool isBusiness) : Customer
{
    public override string Id { get; } = id;

    public override string Type { get; } = isBusiness ? BusinessType : RegisteredType;

    public override bool CanAccess(ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.IsAuthenticated && string.Equals(actor.Name, Id, StringComparison.Ordinal);
    }
}

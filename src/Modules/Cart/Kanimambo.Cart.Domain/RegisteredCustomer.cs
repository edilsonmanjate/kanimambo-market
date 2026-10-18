using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Cart.Domain;

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

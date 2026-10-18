using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Ordering.Application;

internal static class OrderAccess
{
    /// <summary>Back-office sees everything; customers their own orders; guest orders are reached by their random id.</summary>
    public static bool CanView(Order order, ICurrentActor actor) =>
        actor.IsInRole(KnownRoles.Backoffice)
        || order.Customer.Id.Length == 0
        || (actor.IsAuthenticated && order.Customer.Id == actor.Name);

    public static bool CanEdit(DraftOrder draft, ICurrentActor actor) =>
        actor.IsInRole(KnownRoles.Backoffice) || (actor.IsAuthenticated && draft.CustomerId == actor.Name);
}

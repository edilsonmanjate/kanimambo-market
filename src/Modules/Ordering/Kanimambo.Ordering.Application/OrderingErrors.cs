using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

internal static class OrderingErrors
{
    public static Error OrderNotFound(Guid id) => Error.NotFound("ordering.order_not_found", $"Order {id} does not exist.");

    public static Error DraftNotFound(Guid id) => Error.NotFound("ordering.draft_not_found", $"Draft {id} does not exist.");
}

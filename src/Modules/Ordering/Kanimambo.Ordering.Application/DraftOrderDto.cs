using Kanimambo.Ordering.Domain;

namespace Kanimambo.Ordering.Application;

public sealed record DraftOrderDto(Guid Id, Guid SourceOrderId, int Version, string DeliveryCity, IReadOnlyList<OrderLineDto> Lines, Guid? SubmittedOrderId)
{
    public static DraftOrderDto From(DraftOrder draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new DraftOrderDto(draft.Id, draft.SourceOrderId, draft.Version, draft.DeliveryCity, [.. draft.Lines.Select(OrderDto.Line)], draft.SubmittedOrderId);
    }
}

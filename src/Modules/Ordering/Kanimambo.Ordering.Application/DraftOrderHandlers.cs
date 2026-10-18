using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

public sealed class DraftOrderHandlers(
    IOrderRepository orders,
    IDraftOrderRepository drafts,
    IDraftOrderHistory history,
    ICurrentActor actor,
    TimeProvider timeProvider) :
    IRequestHandler<ReorderAsDraft, Result<DraftOrderDto>>,
    IRequestHandler<SetDraftLine, Result<DraftOrderDto>>,
    IRequestHandler<UndoDraftChange, Result<DraftOrderDto>>,
    IRequestHandler<GetDraftOrder, Result<DraftOrderDto>>
{
    public async ValueTask<Result<DraftOrderDto>> Handle(ReorderAsDraft request, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(new OrderId(request.OrderId), cancellationToken);
        if (order is null || !OrderAccess.CanView(order, actor) || order.Customer.Id.Length == 0)
        {
            return OrderingErrors.OrderNotFound(request.OrderId);
        }

        var draft = DraftOrder.CloneFrom(order, Guid.NewGuid(), timeProvider.GetUtcNow());
        if (draft.IsFailure)
        {
            return draft.Error;
        }

        drafts.Add(draft.Value);
        history.Save(draft.Value.Id, draft.Value.CreateMemento());
        return DraftOrderDto.From(draft.Value);
    }

    public async ValueTask<Result<DraftOrderDto>> Handle(SetDraftLine request, CancellationToken cancellationToken)
    {
        var draft = await LoadAsync(request.DraftId, cancellationToken);
        if (draft.IsFailure)
        {
            return draft.Error;
        }

        var changed = draft.Value.SetQuantity(request.ProductId, request.Quantity, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        history.Save(draft.Value.Id, draft.Value.CreateMemento());
        return DraftOrderDto.From(draft.Value);
    }

    /// <summary>Back-office (or the customer) undoes the last edit of a draft.</summary>
    public async ValueTask<Result<DraftOrderDto>> Handle(UndoDraftChange request, CancellationToken cancellationToken)
    {
        var draft = await LoadAsync(request.DraftId, cancellationToken);
        if (draft.IsFailure)
        {
            return draft.Error;
        }

        var previous = (await history.ListAsync(draft.Value.Id, cancellationToken)).LastOrDefault(m => m.Version < draft.Value.Version);
        var source = await orders.GetAsync(new OrderId(draft.Value.SourceOrderId), cancellationToken);
        if (previous is null || source is null)
        {
            return Error.Conflict("ordering.nothing_to_undo", "There is no earlier version of this draft.");
        }

        var restored = draft.Value.Restore(previous, source, timeProvider.GetUtcNow());
        if (restored.IsFailure)
        {
            return restored.Error;
        }

        await history.DiscardAfterAsync(draft.Value.Id, previous.Version, cancellationToken);
        return DraftOrderDto.From(draft.Value);
    }

    public async ValueTask<Result<DraftOrderDto>> Handle(GetDraftOrder request, CancellationToken cancellationToken)
    {
        var draft = await LoadAsync(request.DraftId, cancellationToken);
        return draft.IsSuccess ? DraftOrderDto.From(draft.Value) : draft.Error;
    }

    private async Task<Result<DraftOrder>> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var draft = await drafts.GetAsync(id, cancellationToken);
        return draft is not null && OrderAccess.CanEdit(draft, actor) ? draft : OrderingErrors.DraftNotFound(id);
    }
}

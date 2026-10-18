using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

public sealed class OrderHandlers(IOrderRepository orders, ICurrentActor actor, TimeProvider timeProvider) :
    IRequestHandler<GetOrder, Result<OrderDto>>,
    IRequestHandler<ListMyOrders, Result<IReadOnlyList<OrderDto>>>,
    IRequestHandler<ChangeOrderStatus, Result<OrderDto>>
{
    public async ValueTask<Result<OrderDto>> Handle(GetOrder request, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(new OrderId(request.OrderId), cancellationToken);
        return order is not null && OrderAccess.CanView(order, actor) ? OrderDto.From(order) : OrderingErrors.OrderNotFound(request.OrderId);
    }

    public async ValueTask<Result<IReadOnlyList<OrderDto>>> Handle(ListMyOrders request, CancellationToken cancellationToken) =>
        actor.IsAuthenticated
            ? (await orders.ListByCustomerAsync(actor.Name, cancellationToken)).Select(OrderDto.From).ToList()
            : Error.Unauthorized("ordering.sign_in", "Sign in to see your orders.");

    public async ValueTask<Result<OrderDto>> Handle(ChangeOrderStatus request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderAction>(request.Action, ignoreCase: true, out var action) || !Enum.IsDefined(action))
        {
            return Error.Validation(new FieldError("action", $"Unknown action '{request.Action}'. Use one of: {string.Join(", ", Enum.GetNames<OrderAction>())}."));
        }

        var order = await orders.GetAsync(new OrderId(request.OrderId), cancellationToken);
        if (order is null || !OrderAccess.CanView(order, actor))
        {
            return OrderingErrors.OrderNotFound(request.OrderId);
        }

        var isStaff = actor.IsInRole(KnownRoles.Backoffice);
        var isOwnerAction = action is OrderAction.Cancel or OrderAction.RetryPayment
            && order.Customer.Id.Length > 0 && order.Customer.Id == actor.Name;
        if (!isStaff && !isOwnerAction)
        {
            return Error.Forbidden("ordering.staff_only", "Only back-office staff can change this order's status.");
        }

        var now = timeProvider.GetUtcNow();
        var note = request.Note?.Trim() ?? string.Empty;
        var changed = action switch
        {
            OrderAction.RequestPayment => order.RequestPayment(now),
            OrderAction.ConfirmPayment => order.ConfirmPayment(note, now),
            OrderAction.FailPayment => order.FailPayment(note, now),
            OrderAction.RetryPayment => order.RetryPayment(now),
            OrderAction.StartPreparing => order.StartPreparing(now),
            OrderAction.Dispatch => order.Dispatch(now),
            OrderAction.Deliver => order.Deliver(now),
            OrderAction.Cancel => order.Cancel(note, now),
            OrderAction.Refund => order.Refund(note, now),
            _ => Error.Validation(new FieldError("action", $"Unknown action {action}.")),
        };

        return changed.IsSuccess ? OrderDto.From(order) : changed.Error;
    }
}

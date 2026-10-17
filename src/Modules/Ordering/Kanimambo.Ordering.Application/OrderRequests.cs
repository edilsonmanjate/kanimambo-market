using System.Text.RegularExpressions;
using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

public sealed record OrderLineDto(Guid ProductId, Guid SellerId, string Sku, string Name, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderStatusChangeDto(string From, string To, DateTimeOffset At, string? Reason);

public sealed record OrderDto(
    Guid Id,
    long Number,
    string DisplayNumber,
    string Status,
    IReadOnlyList<string> AllowedActions,
    string CustomerType,
    string DeliveryCity,
    string PaymentMethod,
    string? PaymentReference,
    IReadOnlyList<OrderLineDto> Lines,
    decimal Subtotal,
    string PromotionName,
    decimal Discount,
    decimal ShippingFee,
    decimal Total,
    string DeliveryMethod,
    string? DeliveryDistrict,
    string? PickupPointId,
    DateOnly EstimatedDelivery,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderStatusChangeDto> History)
{
    public static OrderDto From(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var totals = order.Totals;
        return new OrderDto(order.Id.Value, order.Number, order.DisplayNumber, order.Status.ToString(),
            [.. order.AllowedActions.Select(a => a.ToString())], order.Customer.Type, order.DeliveryCity, order.PaymentMethod.ToString(),
            order.PaymentReference, [.. order.Lines.Select(Line)], totals.Subtotal.Amount, totals.PromotionName, totals.Discount.Amount,
            totals.ShippingFee.Amount, totals.Total.Amount, order.Delivery.Method, order.Delivery.District, order.Delivery.PickupPointId,
            order.Delivery.EstimatedDelivery, order.PlacedAt,
            [.. order.History.Select(h => new OrderStatusChangeDto(h.From.ToString(), h.To.ToString(), h.At, h.Reason))]);
    }

    internal static OrderLineDto Line(OrderLine l) => new(l.ProductId, l.SellerId, l.Sku, l.Name, l.Quantity, l.UnitPrice.Amount, l.LineTotal.Amount);
}

public sealed record DraftOrderDto(Guid Id, Guid SourceOrderId, int Version, string DeliveryCity, IReadOnlyList<OrderLineDto> Lines, Guid? SubmittedOrderId)
{
    public static DraftOrderDto From(DraftOrder draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new DraftOrderDto(draft.Id, draft.SourceOrderId, draft.Version, draft.DeliveryCity, [.. draft.Lines.Select(OrderDto.Line)], draft.SubmittedOrderId);
    }
}

/// <summary>Checkout a cart. Retried requests return the same order (see <see cref="CheckoutResult.AlreadyPlaced"/>).</summary>
public sealed record CheckoutCart(Guid CartId, PaymentMethod PaymentMethod, string Phone, string? Nuit, DeliveryChoice? Delivery = null) : ICommand<CheckoutResult>;

public sealed record SubmitDraftOrder(Guid DraftId, PaymentMethod PaymentMethod, string Phone, string? Nuit, DeliveryChoice? Delivery = null) : ICommand<CheckoutResult>;

public sealed record GetOrder(Guid OrderId) : IQuery<OrderDto>;

public sealed record ListMyOrders : IQuery<IReadOnlyList<OrderDto>>;

/// <summary>A lifecycle action (back-office / fulfilment; customers may cancel their own orders).</summary>
/// <remarks><see cref="Action"/> is the name of an <see cref="OrderAction"/>, as it arrives in the URL.</remarks>
public sealed record ChangeOrderStatus(Guid OrderId, string Action, string? Note) : ICommand<OrderDto>;

/// <summary>Start a recurring B2B order by cloning a previous one (Prototype).</summary>
public sealed record ReorderAsDraft(Guid OrderId) : ICommand<DraftOrderDto>;

public sealed record SetDraftLine(Guid DraftId, Guid ProductId, int Quantity) : ICommand<DraftOrderDto>;

public sealed record UndoDraftChange(Guid DraftId) : ICommand<DraftOrderDto>;

public sealed record GetDraftOrder(Guid DraftId) : IQuery<DraftOrderDto>;

public sealed partial class CheckoutValidator : IValidator<CheckoutCart>, IValidator<SubmitDraftOrder>
{
    public IEnumerable<FieldError> Validate(CheckoutCart request) => Validate(request.PaymentMethod, request.Phone);

    public IEnumerable<FieldError> Validate(SubmitDraftOrder request) => Validate(request.PaymentMethod, request.Phone);

    private static IEnumerable<FieldError> Validate(PaymentMethod method, string phone)
    {
        if (method == PaymentMethod.None)
        {
            yield return new FieldError("paymentMethod", "Choose how to pay: MPesa, EMola, MKesh, Card or CashOnDelivery.");
        }

        if (phone is null || !MozambicanPhone().IsMatch(phone.Trim()))
        {
            yield return new FieldError("phone", "Phone numbers use the 258XXXXXXXXX format.");
        }
    }

    [GeneratedRegex(@"^258\d{9}$")]
    private static partial Regex MozambicanPhone();
}

/// <summary>The checkout command has exactly one collaborator: the facade.</summary>
public sealed class CheckoutHandlers(ICheckoutService checkout) :
    IRequestHandler<CheckoutCart, Result<CheckoutResult>>,
    IRequestHandler<SubmitDraftOrder, Result<CheckoutResult>>
{
    public async ValueTask<Result<CheckoutResult>> Handle(CheckoutCart request, CancellationToken cancellationToken) =>
        await checkout.CheckoutCartAsync(request.CartId, new CheckoutDetails(request.PaymentMethod, request.Phone, request.Nuit, request.Delivery ?? DeliveryChoice.Default), cancellationToken);

    public async ValueTask<Result<CheckoutResult>> Handle(SubmitDraftOrder request, CancellationToken cancellationToken) =>
        await checkout.SubmitDraftAsync(request.DraftId, new CheckoutDetails(request.PaymentMethod, request.Phone, request.Nuit, request.Delivery ?? DeliveryChoice.Default), cancellationToken);
}

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

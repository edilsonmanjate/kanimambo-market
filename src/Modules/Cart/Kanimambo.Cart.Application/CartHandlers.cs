using Kanimambo.Cart.Domain;
using Kanimambo.Catalog.Contracts;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application;

public sealed class CartHandlers(
    ICartRepository carts,
    ICartHistory history,
    ICatalogReader catalog,
    ICurrentActor actor,
    TimeProvider timeProvider) :
    IRequestHandler<OpenCart, Result<CartDto>>,
    IRequestHandler<SetCartLine, Result<CartDto>>,
    IRequestHandler<RemoveCartLine, Result<CartDto>>,
    IRequestHandler<ChangeDeliveryCity, Result<CartDto>>,
    IRequestHandler<UndoCartChange, Result<CartDto>>,
    IRequestHandler<RestoreCartSnapshot, Result<CartDto>>,
    IRequestHandler<GetCart, Result<CartDto>>,
    IRequestHandler<ListCartSnapshots, Result<IReadOnlyList<CartSnapshotDto>>>
{
    public ValueTask<Result<CartDto>> Handle(OpenCart request, CancellationToken cancellationToken)
    {
        var cart = ShoppingCart.Open(CartId.New(), Customer.For(actor), request.DeliveryCity, timeProvider.GetUtcNow());
        if (cart.IsFailure)
        {
            return ValueTask.FromResult<Result<CartDto>>(cart.Error);
        }

        carts.Add(cart.Value);
        history.Save(cart.Value.Id, cart.Value.CreateMemento());
        return ValueTask.FromResult<Result<CartDto>>(CartDto.From(cart.Value));
    }

    public async ValueTask<Result<CartDto>> Handle(SetCartLine request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        var item = (await catalog.GetSellableItemsAsync([request.ProductId], cancellationToken)).SingleOrDefault();
        if (item is null)
        {
            return Error.NotFound("cart.product_unavailable", $"Product {request.ProductId} is not available.");
        }

        if (request.Quantity > item.AvailableStock)
        {
            return Error.BusinessRule("cart.insufficient_stock", $"Only {item.AvailableStock} unit(s) of '{item.Name}' available.");
        }

        return await ChangeAsync(cart.Value, c => c.SetQuantity(item.ProductId, item.Sku, item.Name, request.Quantity, timeProvider.GetUtcNow()), cancellationToken);
    }

    public async ValueTask<Result<CartDto>> Handle(RemoveCartLine request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        return cart.IsFailure
            ? cart.Error
            : await ChangeAsync(cart.Value, c => c.Remove(request.ProductId, timeProvider.GetUtcNow()), cancellationToken);
    }

    public async ValueTask<Result<CartDto>> Handle(ChangeDeliveryCity request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        return cart.IsFailure
            ? cart.Error
            : await ChangeAsync(cart.Value, c => c.ChangeDeliveryCity(request.City, timeProvider.GetUtcNow()), cancellationToken);
    }

    public async ValueTask<Result<CartDto>> Handle(UndoCartChange request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        var snapshots = await history.ListAsync(cart.Value.Id, cancellationToken);
        var previous = snapshots.LastOrDefault(s => s.Version < cart.Value.Version);
        return previous is null
            ? Error.Conflict("cart.nothing_to_undo", "There is no earlier state to go back to.")
            : await RestoreAsync(cart.Value, previous, cancellationToken);
    }

    public async ValueTask<Result<CartDto>> Handle(RestoreCartSnapshot request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        var snapshot = (await history.ListAsync(cart.Value.Id, cancellationToken)).SingleOrDefault(s => s.Version == request.Version);
        return snapshot is null
            ? Error.NotFound("cart.snapshot_not_found", $"Snapshot {request.Version} is not available (only the last {CartModule.SnapshotsKept} are kept).")
            : await RestoreAsync(cart.Value, snapshot, cancellationToken);
    }

    public async ValueTask<Result<CartDto>> Handle(GetCart request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        return cart.IsFailure ? cart.Error : CartDto.From(cart.Value);
    }

    public async ValueTask<Result<IReadOnlyList<CartSnapshotDto>>> Handle(ListCartSnapshots request, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        return (await history.ListAsync(cart.Value.Id, cancellationToken)).Select(CartSnapshotDto.From).ToList();
    }

    /// <summary>Applies a change and records the resulting state as a new snapshot.</summary>
    private async Task<Result<CartDto>> ChangeAsync(ShoppingCart cart, Func<ShoppingCart, Result> change, CancellationToken cancellationToken)
    {
        var before = cart.Version;
        var changed = change(cart);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        if (cart.Version != before)
        {
            await history.DiscardAfterAsync(cart.Id, before, cancellationToken);
            history.Save(cart.Id, cart.CreateMemento());
            await history.PruneAsync(cart.Id, cart.Version, CartModule.SnapshotsKept, cancellationToken);
        }

        return CartDto.From(cart);
    }

    private async Task<Result<CartDto>> RestoreAsync(ShoppingCart cart, CartMemento snapshot, CancellationToken cancellationToken)
    {
        var restored = cart.Restore(snapshot, timeProvider.GetUtcNow());
        if (restored.IsFailure)
        {
            return restored.Error;
        }

        await history.DiscardAfterAsync(cart.Id, snapshot.Version, cancellationToken);
        return CartDto.From(cart);
    }
}

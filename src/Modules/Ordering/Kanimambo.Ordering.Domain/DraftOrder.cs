using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

/// <summary>
/// A B2B reorder being prepared — e.g. a restaurant repeating last week's order to the wholesaler.
/// Created as a deep copy of a previous order (Prototype); quantities are then adjusted, with undo (Memento),
/// and the draft is submitted through the same checkout as a cart.
/// </summary>
public sealed class DraftOrder : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = [];

    private DraftOrder()
    {
    }

    private DraftOrder(Guid id, Order prototype, DateTimeOffset now)
        : base(id)
    {
        SourceOrderId = prototype.Id.Value;
        CustomerId = prototype.Customer.Id;
        DeliveryCity = prototype.DeliveryCity;
        PaymentMethod = prototype.PaymentMethod;
        _lines.AddRange(prototype.Lines.Select(l => l.DeepClone()));
        Version = 1;
        CreatedAt = UpdatedAt = now;
    }

    public Guid SourceOrderId { get; private set; }

    public string CustomerId { get; private set; } = string.Empty;

    public string DeliveryCity { get; private set; } = string.Empty;

    public PaymentMethod PaymentMethod { get; private set; }

    public int Version { get; private set; }

    public Guid? SubmittedOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public bool IsSubmitted => SubmittedOrderId is not null;

    /// <summary>Prototype: clone a previous order of the same customer into a new, independent draft.</summary>
    public static Result<DraftOrder> CloneFrom(Order prototype, Guid id, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        return prototype.Customer.IsBusiness
            ? new DraftOrder(id, prototype, now)
            : Error.BusinessRule("ordering.reorder_b2b_only", "Recurring orders are available to business customers.");
    }

    public Result SetQuantity(Guid productId, int quantity, DateTimeOffset now)
    {
        if (IsSubmitted)
        {
            return Error.Conflict("ordering.draft_submitted", "This draft was already submitted.");
        }

        if (quantity < 0)
        {
            return Error.Validation(new FieldError("quantity", "Quantity cannot be negative."));
        }

        var line = _lines.Find(l => l.ProductId == productId);
        if (line is null)
        {
            return Error.NotFound("ordering.draft_line_not_found", "Only products of the original order can be adjusted.");
        }

        if (quantity == 0)
        {
            _lines.Remove(line);
        }
        else
        {
            line.ChangeQuantity(quantity);
        }

        Version++;
        UpdatedAt = now;
        return Result.Success();
    }

    public DraftOrderMemento CreateMemento() =>
        new(Version, UpdatedAt, [.. _lines.Select(l => new DraftOrderMemento.Line(l.ProductId, l.Quantity))]);

    /// <summary>
    /// Restores quantities from a snapshot. Lines removed since then come back as copies of the source
    /// order's lines (the draft never invents products).
    /// </summary>
    public Result Restore(DraftOrderMemento memento, Order source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(memento);
        ArgumentNullException.ThrowIfNull(source);
        if (IsSubmitted)
        {
            return Error.Conflict("ordering.draft_submitted", "This draft was already submitted.");
        }

        var originals = source.Lines.ToDictionary(l => l.ProductId);
        _lines.Clear();
        foreach (var snapshot in memento.Lines)
        {
            var line = originals[snapshot.ProductId].DeepClone();
            line.ChangeQuantity(snapshot.Quantity);
            _lines.Add(line);
        }

        Version = memento.Version;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkSubmitted(Guid orderId, DateTimeOffset now)
    {
        if (IsSubmitted)
        {
            return SubmittedOrderId == orderId ? Result.Success() : Error.Conflict("ordering.draft_submitted", "This draft was already submitted.");
        }

        SubmittedOrderId = orderId;
        UpdatedAt = now;
        return Result.Success();
    }
}

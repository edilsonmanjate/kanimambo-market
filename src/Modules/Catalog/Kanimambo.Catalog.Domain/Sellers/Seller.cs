using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Sellers;

public enum SellerStatus
{
    Active,
    Suspended,
}

public sealed record SellerSuspended(SellerId SellerId, string Reason, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record SellerReactivated(SellerId SellerId, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

/// <summary>A local shop, wholesaler or producer selling on the marketplace.</summary>
public sealed class Seller : AggregateRoot<SellerId>
{
    public const decimal MaxRating = 5m;

    private List<string> _deliveryCities = [];
    private decimal _minimumOrderMzn;

    private Seller()
    {
    }

    private Seller(SellerId id, string name, string city, decimal rating, List<string> deliveryCities, decimal minimumOrderMzn)
        : base(id)
    {
        _minimumOrderMzn = minimumOrderMzn;
        Name = name;
        City = city;
        Rating = rating;
        Status = SellerStatus.Active;
        _deliveryCities = deliveryCities;
    }

    public string Name { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public decimal Rating { get; private set; }

    public SellerStatus Status { get; private set; }

    public string? SuspensionReason { get; private set; }

    public IReadOnlyCollection<string> DeliveryCities => _deliveryCities.AsReadOnly();

    public bool IsActive => Status == SellerStatus.Active;

    /// <summary>Smallest order (in MZN, per seller) the seller accepts; wholesalers typically set one.</summary>
    public Money MinimumOrder => Money.Mzn(_minimumOrderMzn);

    public static Result<Seller> Register(SellerId id, string name, string city, decimal rating, IEnumerable<string> deliveryCities, decimal minimumOrderMzn = 0m)
    {
        ArgumentNullException.ThrowIfNull(deliveryCities);

        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new FieldError(nameof(name), "Seller name is required."));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            errors.Add(new FieldError(nameof(city), "Seller city is required."));
        }

        if (rating is < 0m or > MaxRating)
        {
            errors.Add(new FieldError(nameof(rating), $"Rating must be between 0 and {MaxRating}."));
        }

        if (minimumOrderMzn < 0m)
        {
            errors.Add(new FieldError(nameof(minimumOrderMzn), "Minimum order cannot be negative."));
        }

        var cities = deliveryCities
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        return new Seller(id, name.Trim(), city.Trim(), rating, cities, minimumOrderMzn);
    }

    public bool DeliversTo(string city) => _deliveryCities.Contains(city, StringComparer.OrdinalIgnoreCase);

    public Result Suspend(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation(new FieldError(nameof(reason), "A suspension needs a reason."));
        }

        if (Status == SellerStatus.Suspended)
        {
            return Error.Conflict("catalog.seller_already_suspended", $"Seller '{Name}' is already suspended.");
        }

        Status = SellerStatus.Suspended;
        SuspensionReason = reason.Trim();
        Raise(new SellerSuspended(Id, SuspensionReason, now));
        return Result.Success();
    }

    public Result Reactivate(DateTimeOffset now)
    {
        if (Status != SellerStatus.Suspended)
        {
            return Error.Conflict("catalog.seller_not_suspended", $"Seller '{Name}' is not suspended.");
        }

        Status = SellerStatus.Active;
        SuspensionReason = null;
        Raise(new SellerReactivated(Id, now));
        return Result.Success();
    }
}

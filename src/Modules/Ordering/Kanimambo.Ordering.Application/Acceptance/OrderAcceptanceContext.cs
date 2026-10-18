using Kanimambo.Catalog.Contracts;
using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Application.Acceptance;

/// <summary>Everything the acceptance checks look at, gathered once by checkout.</summary>
public sealed record OrderAcceptanceContext(
    OrderCustomer Customer,
    string DeliveryCity,
    PaymentMethod PaymentMethod,
    IReadOnlyList<AcceptanceLine> Lines,
    IReadOnlyDictionary<Guid, SellerSnapshot> Sellers,
    Money Total);

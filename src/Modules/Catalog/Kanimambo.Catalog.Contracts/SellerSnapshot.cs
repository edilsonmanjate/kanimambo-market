using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Contracts;

/// <summary>What another module may know about a seller: whether it sells, where it is based and delivers, its minimum order.</summary>
public sealed record SellerSnapshot(Guid SellerId, string Name, bool IsActive, IReadOnlyList<string> DeliveryCities, Money MinimumOrder, string City);

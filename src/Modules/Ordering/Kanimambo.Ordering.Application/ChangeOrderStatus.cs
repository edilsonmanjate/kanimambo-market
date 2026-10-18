using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

/// <summary>A lifecycle action (back-office / fulfilment; customers may cancel their own orders).</summary>
/// <remarks><see cref="Action"/> is the name of an <see cref="OrderAction"/>, as it arrives in the URL.</remarks>
public sealed record ChangeOrderStatus(Guid OrderId, string Action, string? Note) : ICommand<OrderDto>;

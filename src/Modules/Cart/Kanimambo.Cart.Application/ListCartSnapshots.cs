using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

public sealed record ListCartSnapshots(Guid CartId) : IQuery<IReadOnlyList<CartSnapshotDto>>;

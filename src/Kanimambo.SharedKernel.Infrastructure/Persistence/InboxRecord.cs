using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

/// <summary>A processed <see cref="IIdempotentCommand"/> key, stored in the owning module's schema.</summary>
public sealed class InboxRecord
{
    public required Guid IdempotencyKey { get; init; }

    public required string CommandName { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}

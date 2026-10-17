using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.Domain;

/// <summary>Something that happened in the domain that other parts of the system may react to.</summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base record for domain events. The time is passed in by the aggregate (which receives it
/// from an injected <see cref="TimeProvider"/>), never read from the system clock here.
/// </summary>
public abstract record DomainEvent(DateTimeOffset OccurredAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7(OccurredAt);
}

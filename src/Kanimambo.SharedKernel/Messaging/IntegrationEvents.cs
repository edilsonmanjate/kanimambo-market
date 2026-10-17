using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>
/// An event other modules may subscribe to. Published through the owning module's outbox, so it is
/// delivered at least once even if the process crashes right after the change was committed.
/// Subscribers must be idempotent (see <see cref="IIdempotentCommand"/>).
/// </summary>
public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>Translates a module's internal domain events into the integration events it publishes.</summary>
public interface IIntegrationEventMapper
{
    IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent);
}

using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.Domain;

/// <summary>Something that happened in the domain that other parts of the system may react to.</summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

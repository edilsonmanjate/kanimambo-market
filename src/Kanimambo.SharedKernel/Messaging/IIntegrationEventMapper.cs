using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>Translates a module's internal domain events into the integration events it publishes.</summary>
public interface IIntegrationEventMapper
{
    IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent);
}

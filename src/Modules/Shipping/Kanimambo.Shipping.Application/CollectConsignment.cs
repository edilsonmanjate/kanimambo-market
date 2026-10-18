using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Application;

/// <summary>Pickup-point staff hand the parcels to the customer.</summary>
public sealed record CollectConsignment(Guid ConsignmentId) : ICommand<ConsignmentDto>;

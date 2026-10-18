using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Application;

public sealed record GetConsignmentForOrder(Guid OrderId) : IQuery<ConsignmentDto>;

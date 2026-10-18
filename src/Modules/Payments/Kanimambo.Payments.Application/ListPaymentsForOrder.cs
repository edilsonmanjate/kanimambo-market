using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Application;

public sealed record ListPaymentsForOrder(Guid OrderId) : IQuery<IReadOnlyList<PaymentDto>>;

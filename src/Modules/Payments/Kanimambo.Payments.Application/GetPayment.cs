using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Application;

public sealed record GetPayment(Guid PaymentId) : IQuery<PaymentDto>;

using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Application;

/// <summary>Start collecting an order's payment. Idempotent: one payment per (order, attempt).</summary>
public sealed record StartPayment(Guid IdempotencyKey, Guid OrderId, long OrderNumber, int Attempt, string Method, string Phone, decimal AmountMzn)
    : IIdempotentCommand;

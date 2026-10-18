using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

/// <summary>Credit every invoice of a refunded order; cancel the ones that were never issued.</summary>
public sealed record CreditRefundedOrder(Guid IdempotencyKey, Guid OrderId, string Reason) : IIdempotentCommand;

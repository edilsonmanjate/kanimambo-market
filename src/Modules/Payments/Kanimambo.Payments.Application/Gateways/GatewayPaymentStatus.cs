using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application.Gateways;

/// <summary>A payment's state as the operator sees it (from a status query or a callback).</summary>
public sealed record GatewayPaymentStatus(
    Guid PaymentId,
    GatewayPaymentState State,
    string OperatorTransactionId,
    PaymentFailureReason? FailureReason = null,
    string? NativeCode = null)
{
    public bool IsFinal => State != GatewayPaymentState.Pending;
}

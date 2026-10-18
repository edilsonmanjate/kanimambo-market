namespace Kanimambo.Payments.Domain;

/// <summary>Why a payment did not happen — one vocabulary, whatever the operator's native code was.</summary>
public enum PaymentFailureReason
{
    InsufficientBalance,
    DuplicateTransaction,
    InvalidNumber,
    Timeout,
    Rejected,
    GatewayUnavailable,
    UnknownOperator,
    WalletMismatch,
    UnsupportedMethod,
}

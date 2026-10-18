namespace Kanimambo.Ordering.Domain;

/// <summary>Everything that can be asked of an order.</summary>
public enum OrderAction
{
    RequestPayment,
    ConfirmPayment,
    FailPayment,
    RetryPayment,
    StartPreparing,
    Dispatch,
    Deliver,
    Cancel,
    Refund,
}

using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    long OrderNumber,
    int Attempt,
    string Method,
    string? Operator,
    string Phone,
    decimal Amount,
    string Status,
    string? OperatorTransactionId,
    string? FailureReason,
    string? NativeCode,
    IReadOnlyList<PaymentTransitionDto> History)
{
    public static PaymentDto From(Payment p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new PaymentDto(p.Id, p.OrderId, p.OrderNumber, p.Attempt, p.Method.ToString(), p.Operator?.ToString(), p.Phone, p.Amount.Amount,
            p.Status.ToString(), p.OperatorTransactionId, p.FailureReason?.ToString(), p.NativeCode,
            [.. p.History.Select(h => new PaymentTransitionDto(h.From.ToString(), h.To.ToString(), h.At, h.Detail))]);
    }
}

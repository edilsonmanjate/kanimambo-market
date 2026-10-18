using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Gateways;

/// <summary>An operator error translated into the shared vocabulary, keeping the native code for support.</summary>
public sealed record GatewayError : Error
{
    public GatewayError(PaymentFailureReason reason, string? nativeCode, string message)
        : base($"payments.{ToSnakeCase(reason)}", message, reason == PaymentFailureReason.GatewayUnavailable ? ErrorType.Unavailable : ErrorType.BusinessRule)
    {
        Reason = reason;
        NativeCode = nativeCode;
    }

    public PaymentFailureReason Reason { get; }

    public string? NativeCode { get; }

    public bool IsTransient => Reason is PaymentFailureReason.GatewayUnavailable or PaymentFailureReason.Timeout;

    private static string ToSnakeCase(PaymentFailureReason reason) =>
        string.Concat(reason.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : char.ToLowerInvariant(c).ToString()));
}

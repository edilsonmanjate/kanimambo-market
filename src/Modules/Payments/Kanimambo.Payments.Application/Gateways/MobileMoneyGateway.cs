using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Gateways;

/// <summary>What we ask an operator: "charge this phone this amount, call me back here". <see cref="PaymentId"/> is our reference.</summary>
public sealed record MobileMoneyRequest(Guid PaymentId, string Phone, Money Amount, Uri CallbackUrl, string Description);

/// <summary>The operator accepted the request and pushed a PIN prompt to the customer's phone.</summary>
public sealed record GatewayAcceptance(string OperatorTransactionId);

public enum GatewayPaymentState
{
    Pending,
    Confirmed,
    Failed,
}

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

/// <summary>
/// Adapter target: the one interface the application uses for every mobile-money operator. M-Pesa, e-Mola
/// and mKesh speak different formats, codes and signatures; each has an adapter that translates to this.
/// </summary>
public interface IMobileMoneyGateway
{
    MobileOperator Operator { get; }

    /// <summary>Asks the operator to push a PIN prompt. Failures are <see cref="GatewayError"/>s.</summary>
    Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken);

    Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Verifies the operator's signature and translates its callback payload.</summary>
    Result<GatewayPaymentStatus> ParseCallback(string body, string? signature);
}

public interface IMobileMoneyGatewayProvider
{
    /// <summary>The gateway for an operator, with its decorators applied.</summary>
    IMobileMoneyGateway For(MobileOperator mobileOperator);
}

/// <summary>Card payments through the SIMO network. Locally a simulated acquirer.</summary>
public interface ICardAcquirer
{
    Task<Result<string>> ChargeAsync(Guid paymentId, Money amount, CancellationToken cancellationToken);
}

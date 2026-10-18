namespace Kanimambo.Payments.Application.Gateways;

/// <summary>The operator accepted the request and pushed a PIN prompt to the customer's phone.</summary>
public sealed record GatewayAcceptance(string OperatorTransactionId);

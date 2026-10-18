using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Application;

/// <summary>An operator's asynchronous answer (the customer typed the PIN, or it failed).</summary>
public sealed record HandleGatewayCallback(string Operator, string Body, string? Signature) : ICommand;

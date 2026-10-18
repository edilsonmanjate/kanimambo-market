namespace Kanimambo.Simulators.Messaging;

public sealed record EmailRequest(string To, string Subject, string Html);

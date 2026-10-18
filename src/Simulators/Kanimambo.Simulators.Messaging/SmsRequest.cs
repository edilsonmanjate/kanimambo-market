namespace Kanimambo.Simulators.Messaging;

public sealed record SmsRequest(string To, string Sender, string Text);

namespace Kanimambo.Notifications.Domain;

/// <summary>Who a message goes to: always a phone number, sometimes an e-mail address.</summary>
public sealed record Recipient(string Phone, string? Email);

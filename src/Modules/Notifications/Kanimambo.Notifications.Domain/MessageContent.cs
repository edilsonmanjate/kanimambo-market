namespace Kanimambo.Notifications.Domain;

/// <summary>
/// What a message says, independent of how it travels: a subject, the one-line essentials (all an SMS can carry),
/// the details richer channels show, and an optional link.
/// </summary>
public sealed record MessageContent(string Subject, string Headline, string Details, Uri? Link);

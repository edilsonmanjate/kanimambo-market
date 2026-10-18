namespace Kanimambo.Notifications.Api.Controllers;

/// <summary>Channels in order of preference, e.g. <c>["WhatsApp", "Sms"]</c>; e-mail needs an address.</summary>
public sealed record ContactPreferencesRequest(string? Email, IReadOnlyList<string> Channels);

namespace Kanimambo.Notifications.Application;

public sealed record ContactPreferencesDto(string Phone, string? Email, IReadOnlyList<string> Channels);

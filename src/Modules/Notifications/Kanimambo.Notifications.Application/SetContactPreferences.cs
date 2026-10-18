using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Notifications.Application;

public sealed record SetContactPreferences(string Phone, string? Email, IReadOnlyList<string> Channels) : ICommand<ContactPreferencesDto>;

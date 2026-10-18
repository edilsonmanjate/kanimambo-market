using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Notifications.Application;

public sealed record GetContactPreferences(string Phone) : IQuery<ContactPreferencesDto>;

using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed record OrderPaid(Guid OrderId) : INotification;

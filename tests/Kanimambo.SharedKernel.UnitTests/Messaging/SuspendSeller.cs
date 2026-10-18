using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed record SuspendSeller(Guid SellerId) : ICommand;

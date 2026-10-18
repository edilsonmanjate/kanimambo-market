using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed record ConfirmMobileMoneyPayment(Guid IdempotencyKey, bool Succeeds = true) : IIdempotentCommand;

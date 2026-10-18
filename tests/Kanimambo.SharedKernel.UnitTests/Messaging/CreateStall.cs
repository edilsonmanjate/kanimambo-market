using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed record CreateStall(string Name, string City) : ICommand<Guid>;

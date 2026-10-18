using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

public sealed record UndoDraftChange(Guid DraftId) : ICommand<DraftOrderDto>;

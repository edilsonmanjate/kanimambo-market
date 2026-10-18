using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

public sealed record SetDraftLine(Guid DraftId, Guid ProductId, int Quantity) : ICommand<DraftOrderDto>;

using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

public sealed record GetDraftOrder(Guid DraftId) : IQuery<DraftOrderDto>;

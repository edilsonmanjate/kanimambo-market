using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Promotions.Application;

public sealed record ListPromotions(bool ActiveOnly = false) : IQuery<IReadOnlyList<PromotionDto>>;

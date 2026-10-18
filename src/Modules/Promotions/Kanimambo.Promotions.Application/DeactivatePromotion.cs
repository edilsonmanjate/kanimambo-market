using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Promotions.Application;

public sealed record DeactivatePromotion(Guid PromotionId) : ICommand<PromotionDto>;

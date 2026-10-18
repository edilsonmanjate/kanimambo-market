using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Promotions.Application;

internal sealed record AppliedPromotion(Guid PromotionId, string Name, Money Discount) : IAppliedPromotion;

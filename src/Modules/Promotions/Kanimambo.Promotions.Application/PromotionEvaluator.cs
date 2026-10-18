using Kanimambo.Promotions.Contracts;
using Kanimambo.Promotions.Domain.Rules;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Promotions.Application;

/// <summary>
/// Evaluates every active promotion's rule against the cart and returns the one giving the largest discount
/// (earliest created wins a tie), or the <see cref="NoPromotion"/> null object.
/// </summary>
internal sealed class PromotionEvaluator(IPromotionRepository promotions, TimeProvider timeProvider) : IPromotionEvaluator
{
    public async Task<IAppliedPromotion> BestPromotionAsync(PromotionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var now = MozambiqueTime.Now(timeProvider);
        var today = DateOnly.FromDateTime(now.DateTime);
        var ruleContext = ToRuleContext(context, now);

        var candidates = await promotions.ListAsync(activeOnly: true, cancellationToken);

        return candidates
            .Where(p => p.AppliesTo(ruleContext, today))
            .OrderBy(p => p.CreatedAt)
            .Select(p => (IAppliedPromotion)new AppliedPromotion(p.Id, p.Name, p.Discount.AmountFor(context.CartTotal)))
            .Aggregate(
                (IAppliedPromotion)NoPromotion.Instance,
                (best, next) => next.Discount > best.Discount ? next : best);
    }

    internal static RuleContext ToRuleContext(PromotionContext context, DateTimeOffset mozambiqueNow) => new(new Dictionary<string, RuleValue>
    {
        [RuleVariables.CartTotal] = RuleValue.Of(context.CartTotal.Amount),
        [RuleVariables.CartItemCount] = RuleValue.Of(context.ItemCount),
        [RuleVariables.CartCategories] = RuleValue.Of([.. context.Categories]),
        [RuleVariables.CustomerCity] = RuleValue.Of(context.CustomerCity),
        [RuleVariables.CustomerType] = RuleValue.Of(context.CustomerType),
        [RuleVariables.PaymentMethod] = RuleValue.Of(context.PaymentMethod.ToString()),
        [RuleVariables.TodayWeekday] = RuleValue.Of(mozambiqueNow.DayOfWeek.ToString()),
    });
}

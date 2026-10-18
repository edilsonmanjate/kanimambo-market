using Kanimambo.Promotions.Domain;
using Kanimambo.Promotions.Domain.Rules;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Promotions.Application;

public sealed class PromotionHandlers(IPromotionRepository promotions, ICurrentActor actor, TimeProvider timeProvider) :
    IRequestHandler<CreatePromotion, Result<PromotionDto>>,
    IRequestHandler<DeactivatePromotion, Result<PromotionDto>>,
    IRequestHandler<ListPromotions, Result<IReadOnlyList<PromotionDto>>>,
    IRequestHandler<ValidatePromotionRule, Result<RuleValidationDto>>,
    IRequestHandler<ListRuleVariables, Result<IReadOnlyList<RuleVariableDto>>>
{
    public ValueTask<Result<PromotionDto>> Handle(CreatePromotion request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DiscountKind>(request.DiscountKind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return ValueTask.FromResult<Result<PromotionDto>>(Error.Validation(
                new FieldError("discountKind", $"Unknown discount kind '{request.DiscountKind}'; use Percentage or FixedAmount.")));
        }

        var discount = PromotionDiscount.Create(kind, request.DiscountValue);
        if (discount.IsFailure)
        {
            return ValueTask.FromResult<Result<PromotionDto>>(discount.Error);
        }

        var promotion = Promotion.Create(request.Name, request.Rule, discount.Value, request.ValidFrom, request.ValidUntil, actor.Name, timeProvider.GetUtcNow());
        if (promotion.IsFailure)
        {
            return ValueTask.FromResult<Result<PromotionDto>>(promotion.Error);
        }

        promotions.Add(promotion.Value);
        return ValueTask.FromResult<Result<PromotionDto>>(PromotionDto.From(promotion.Value));
    }

    public async ValueTask<Result<PromotionDto>> Handle(DeactivatePromotion request, CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetAsync(request.PromotionId, cancellationToken);
        if (promotion is null)
        {
            return Error.NotFound("promotions.not_found", $"Promotion {request.PromotionId} does not exist.");
        }

        var deactivated = promotion.Deactivate();
        return deactivated.IsSuccess ? PromotionDto.From(promotion) : deactivated.Error;
    }

    public async ValueTask<Result<IReadOnlyList<PromotionDto>>> Handle(ListPromotions request, CancellationToken cancellationToken) =>
        (await promotions.ListAsync(request.ActiveOnly, cancellationToken)).Select(PromotionDto.From).ToList();

    public ValueTask<Result<RuleValidationDto>> Handle(ValidatePromotionRule request, CancellationToken cancellationToken)
    {
        var (rule, diagnostic) = RuleParser.Diagnose(request.Rule);
        var dto = diagnostic is null
            ? new RuleValidationDto(true, rule!.ToString(), null)
            : new RuleValidationDto(false, null, new RuleDiagnosticDto(diagnostic.Position, diagnostic.Length, diagnostic.Message, diagnostic.Excerpt(request.Rule ?? string.Empty)));
        return ValueTask.FromResult<Result<RuleValidationDto>>(dto);
    }

    public ValueTask<Result<IReadOnlyList<RuleVariableDto>>> Handle(ListRuleVariables request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<RuleVariableDto>>>(
            RuleVariables.All.Select(v => new RuleVariableDto(v.Name, v.Type.ToString(), v.Description)).ToList());
}

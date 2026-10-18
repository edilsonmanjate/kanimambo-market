using Kanimambo.Promotions.Application;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Promotions.Api.Controllers;

/// <summary>Promotions written by marketing as rules, e.g. <c>cart.total &gt;= 2500 AND customer.city = "Maputo"</c>.</summary>
[Route("api/promotions")]
[Authorize(Policy = AuthorizationPolicies.Backoffice)]
public sealed class PromotionsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PromotionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListPromotions(activeOnly), cancellationToken));

    /// <summary>Creates a promotion. An invalid rule is a 400 whose message gives the position of the problem.</summary>
    [HttpPost]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreatePromotionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(
            new CreatePromotion(request.Name, request.Rule, request.DiscountKind, request.DiscountValue, request.ValidFrom, request.ValidUntil),
            cancellationToken));
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new DeactivatePromotion(id), cancellationToken));

    /// <summary>Checks a rule while it is being written: canonical form, or the error with position and a caret excerpt.</summary>
    [HttpPost("rules/validate")]
    [ProducesResponseType<RuleValidationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(ValidateRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new ValidatePromotionRule(request.Rule), cancellationToken));
    }

    [HttpGet("rules/variables")]
    [ProducesResponseType<IReadOnlyList<RuleVariableDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Variables(CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListRuleVariables(), cancellationToken));
}

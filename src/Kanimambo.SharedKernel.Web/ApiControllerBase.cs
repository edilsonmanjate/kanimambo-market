using Kanimambo.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kanimambo.SharedKernel.Web;

/// <summary>
/// Base for every controller. Controllers stay thin: bind input, send through IMediator, and turn the
/// <see cref="Result"/> into HTTP. Failures always become RFC 9457 ProblemDetails with a stable
/// <c>code</c> extension, so clients never parse human-readable messages.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    public const string ErrorCodeExtension = "code";

    protected IActionResult OkOrProblem<TValue>(Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
    }

    protected IActionResult NoContentOrProblem(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? NoContent() : ProblemFrom(result.Error);
    }

    protected IActionResult CreatedOrProblem<TValue>(Result<TValue> result, string actionName, Func<TValue, object> routeValues)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(routeValues);
        return result.IsSuccess
            ? CreatedAtAction(actionName, routeValues(result.Value), result.Value)
            : ProblemFrom(result.Error);
    }

    protected IActionResult ProblemFrom(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error is ValidationError validation)
        {
            var modelState = new ModelStateDictionary();
            foreach (var failure in validation.Errors)
            {
                modelState.AddModelError(failure.Field, failure.Message);
            }

            var validationProblem = ProblemDetailsFactory.CreateValidationProblemDetails(
                HttpContext, modelState, ErrorHttpMapping.ToStatusCode(error.Type), detail: error.Message);
            validationProblem.Extensions[ErrorCodeExtension] = error.Code;
            return new ObjectResult(validationProblem) { StatusCode = validationProblem.Status };
        }

        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, ErrorHttpMapping.ToStatusCode(error.Type), detail: error.Message);
        problem.Extensions[ErrorCodeExtension] = error.Code;
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}

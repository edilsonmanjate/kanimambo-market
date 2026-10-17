using System.Text.Json.Serialization;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Kanimambo.Api.ErrorHandling;

internal static class ProblemDetailsServiceCollectionExtensions
{
    public static IServiceCollection AddKanimamboProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}");

        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }

    public static IMvcBuilder AddKanimamboControllers(this IServiceCollection services) =>
        services
            .AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
            {
                // Model-binding failures use the same shape as ValidationError results from the Mediator.
                var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
                problem.Extensions[ApiControllerBase.ErrorCodeExtension] = ValidationError.ValidationCode;
                return new BadRequestObjectResult(problem);
            });
}

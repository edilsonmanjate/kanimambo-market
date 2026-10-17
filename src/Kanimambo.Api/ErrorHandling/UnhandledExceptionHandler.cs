using Kanimambo.SharedKernel.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Api.ErrorHandling;

/// <summary>
/// Last line of defence: turns exceptions (bugs, outages) into a ProblemDetails response without
/// leaking internals. Expected business failures never get here; they travel as Result values.
/// </summary>
internal sealed partial class UnhandledExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            BadHttpRequestException badRequest => (badRequest.StatusCode, "The request is malformed.", "request.malformed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "server.unexpected_error"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { [ApiControllerBase.ErrorCodeExtension] = code },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}

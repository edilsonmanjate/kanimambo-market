using Kanimambo.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace Kanimambo.SharedKernel.Web;

/// <summary>Single place where business error categories become HTTP status codes.</summary>
public static class ErrorHttpMapping
{
    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Failure => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped error type."),
    };
}

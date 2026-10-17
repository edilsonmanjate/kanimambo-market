namespace Kanimambo.SharedKernel.Results;

/// <summary>
/// A typed, expected business error. <see cref="Code"/> is stable and machine-readable
/// (e.g. <c>ordering.nuit_invalid</c>); <see cref="Message"/> is for humans.
/// </summary>
public record Error(string Code, string Message, ErrorType Type)
{
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static ValidationError Validation(params IReadOnlyList<FieldError> errors) => new(errors);
}

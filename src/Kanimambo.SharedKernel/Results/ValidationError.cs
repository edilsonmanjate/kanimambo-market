namespace Kanimambo.SharedKernel.Results;

public sealed record FieldError(string Field, string Message);

/// <summary>An <see cref="Error"/> that carries one or more field-level failures.</summary>
public sealed record ValidationError : Error
{
    public const string ValidationCode = "validation.failed";

    public ValidationError(IReadOnlyList<FieldError> errors)
        : base(ValidationCode, "One or more validation errors occurred.", ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A validation error needs at least one field error.", nameof(errors));
        }

        Errors = errors;
    }

    public IReadOnlyList<FieldError> Errors { get; }
}

namespace Kanimambo.SharedKernel.Results;

/// <summary>
/// Outcome of an operation that can fail for an expected business reason.
/// Exceptions remain reserved for exceptional situations (bugs, infrastructure outages).
/// </summary>
public class Result : IResultFactory<Result>
{
    private static readonly Result SuccessInstance = new(null);

    private readonly Error? _error;

    protected Result(Error? error) => _error = error;

    public bool IsSuccess => _error is null;

    public bool IsFailure => !IsSuccess;

    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => SuccessInstance;

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Failure(error);

    public static Result FromError(Error error) => Failure(error);

    static Result IResultFactory<Result>.CreateFailure(Error error) => Failure(error);

    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(Error);
    }
}

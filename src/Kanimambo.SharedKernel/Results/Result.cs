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

/// <summary>A <see cref="Result"/> that carries a value when successful.</summary>
public sealed class Result<TValue> : Result, IResultFactory<Result<TValue>>
{
    private readonly TValue? _value;

    private Result(TValue? value, Error? error)
        : base(error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error.Code}).");

    public static Result<TValue> Success(TValue value) => new(value, null);

    public static new Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TValue>(default, error);
    }

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure(error);

    public static Result<TValue> FromValue(TValue value) => Success(value);

    public static new Result<TValue> FromError(Error error) => Failure(error);

    static Result<TValue> IResultFactory<Result<TValue>>.CreateFailure(Error error) => Failure(error);

    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }
}

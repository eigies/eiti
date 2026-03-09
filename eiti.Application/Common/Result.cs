namespace eiti.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool success, Error error)
    {
        if (success && error != Error.None)
        {
            throw new InvalidOperationException("Success result cannot have an error.");
        }

        if (!success && error == Error.None)
        {
            throw new InvalidOperationException("Failure result must have an error.");
        }

        IsSuccess = success;
        Error = error;
    }

    public static Result Success()
        => new(true, Error.None);

    public static Result Failure(Error error)
        => new(false, error);

    public static Result<T> Success<T>(T value)
        => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error)
        => Result<T>.Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    public T Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("Cannot access value of a failed result.");
            }

            return _value!;
        }
    }

    private Result(T value)
        : base(true, Error.None)
    {
        _value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        _value = default;
    }

    public static Result<T> Success(T value)
        => new(value);

    public static new Result<T> Failure(Error error)
        => new(error);
}

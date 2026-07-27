namespace TmsApi.Application.Common;

public readonly record struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;
    public bool IsSuccess { get; }

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value);

    public static Result<TValue, TError> Failure(TError error) => new(error);

    public TValue Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("Result is failure; callMatch instead of Value.");
    public TError Error =>
        !IsSuccess
            ? _error!
            : throw new InvalidOperationException("Result is success; callMatch instead of Error.");

    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<TError, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}

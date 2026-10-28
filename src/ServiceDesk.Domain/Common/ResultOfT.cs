namespace ServiceDesk.Domain.Common;

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue value)
        : base(null)
    {
        _value = value;
    }

    internal Result(Error error)
        : base(error)
    {
    }

    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}

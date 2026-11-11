namespace SponsorPulse.Domain.Primitives;

public class Result<T>
{
    public T? Value { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    protected Result(T? value, bool isSuccess, string? errorMessage)
    {
        Value = value;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new(value, true, null);

    public static Result<T> Failure(string errorMessage) =>
        new(default, false, errorMessage);
}

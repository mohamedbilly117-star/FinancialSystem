namespace ERP.Application.Common.Models;

/// <summary>
/// A simple, explicit success/failure result, used as the return type for
/// use cases where throwing an exception would be too heavy for an
/// *expected* business outcome (e.g. "distribution template does not sum
/// to 100%" is an everyday configuration mistake an administrator should
/// be told about calmly, not a stack trace). Reserve actual exceptions
/// (ValidationException, ForbiddenAccessException, DomainException) for
/// truly exceptional / programming-error conditions, per common Clean
/// Architecture guidance to avoid using exceptions for ordinary control
/// flow in high-volume transactional code paths (relevant given Prompt 0's
/// "unlimited transactions" / performance principles).
/// </summary>
public class Result
{
    protected Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; }

    public string[] Errors { get; }

    public static Result Success() => new(true, Array.Empty<string>());

    public static Result Failure(IEnumerable<string> errors) => new(false, errors);

    public static Result Failure(string error) => new(false, new[] { error });
}

/// <summary>Generic variant of <see cref="Result"/> that also carries a return value on success.</summary>
public class Result<T> : Result
{
    private Result(bool succeeded, T? value, IEnumerable<string> errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());

    public static new Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors);

    public static new Result<T> Failure(string error) => new(false, default, new[] { error });
}

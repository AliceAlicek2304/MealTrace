namespace MealTrace.Application.Abstractions;

public enum ErrorKind { Validation, Unauthenticated, Forbidden, NotFound, Conflict, Unexpected }

public sealed record Failure(ErrorKind Kind, string? Message = null);

public readonly record struct Unit
{
    public static Unit Value => default;
}

/// <summary>Business outcome. Status codes, response headers and resource URLs belong to the API.</summary>
public sealed class Result<T>
{
    private Result(T value) { Value = value; }
    private Result(Failure error) { Error = error; }
    public bool IsSuccess => Error is null;
    public T? Value { get; }
    public Failure? Error { get; }
    public static Result<T> Success(T value) => new(value);
    public static implicit operator Result<T>(Failure error) => new(error);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Failure Invalid(string? message = null) => new(ErrorKind.Validation, message);
    public static Failure Unauthenticated() => new(ErrorKind.Unauthenticated);
    public static Failure Forbidden() => new(ErrorKind.Forbidden);
    public static Failure NotFound(string? message = null) => new(ErrorKind.NotFound, message);
    public static Failure Conflict(string? message = null) => new(ErrorKind.Conflict, message);
    public static Failure Unexpected(string message) => new(ErrorKind.Unexpected, message);
}

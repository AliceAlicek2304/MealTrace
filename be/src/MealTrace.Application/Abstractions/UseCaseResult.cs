namespace MealTrace.Application.Abstractions;

/// <summary>A transport-independent outcome. Only the API turns it into an HTTP response.</summary>
public sealed record UseCaseResult(int Status, object? Value = null, string? Location = null, string? Detail = null)
{
    public static UseCaseResult Ok(object? value) => new(200, value);
    public static UseCaseResult Created(string location, object? value) => new(201, value, location);
    public static UseCaseResult NoContent() => new(204);
    public static UseCaseResult BadRequest(object? value = null) => new(400, value);
    public static UseCaseResult Unauthorized() => new(401);
    public static UseCaseResult Forbid() => new(403);
    public static UseCaseResult NotFound(object? value = null) => new(404, value);
    public static UseCaseResult Conflict(object? value = null) => new(409, value);
    public static UseCaseResult Problem(string detail) => new(500, Detail: detail);
}

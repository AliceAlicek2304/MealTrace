using MealTrace.Application.Abstractions;

namespace MealTrace.Api.Features;

internal static class UseCaseResultExtensions
{
    public static IResult ToHttpResult(this UseCaseResult result) => result.Status switch
    {
        200 => Results.Ok(result.Value),
        201 => Results.Created(result.Location, result.Value),
        204 => Results.NoContent(),
        400 => Results.BadRequest(result.Value),
        401 => Results.Unauthorized(),
        403 => Results.Forbid(),
        404 => Results.NotFound(result.Value),
        409 => Results.Conflict(result.Value),
        500 => Results.Problem(result.Detail),
        _ => throw new InvalidOperationException($"Unsupported use-case status {result.Status}.")
    };
}

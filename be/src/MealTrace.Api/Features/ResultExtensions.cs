using MealTrace.Application.Abstractions;
using MealTrace.Application.Dtos.Common;

namespace MealTrace.Api.Features;

internal static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) => result.Error is { } error
        ? error.ToHttpResult()
        : typeof(T) == typeof(Unit) ? Results.NoContent() : Results.Ok(result.Value);

    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string> location) => result.Error is { } error
        ? error.ToHttpResult()
        : Results.Created(location(result.Value!), result.Value);

    public static IResult ToHttpResult(this Failure error)
    {
        var body = error.Message is null ? null : new MessageResponse(error.Message);
        return error.Kind switch
        {
            ErrorKind.Validation => Results.BadRequest(body),
            ErrorKind.Unauthenticated => Results.Unauthorized(),
            ErrorKind.Forbidden => Results.Forbid(),
            ErrorKind.NotFound => Results.NotFound(body),
            ErrorKind.Conflict => Results.Conflict(body),
            ErrorKind.Unexpected => Results.Problem(error.Message),
            _ => throw new InvalidOperationException("Unsupported application error.")
        };
    }
}

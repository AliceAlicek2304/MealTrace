namespace MealTrace.Application.Dtos.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUser User);

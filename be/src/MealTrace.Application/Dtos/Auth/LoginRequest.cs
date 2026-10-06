namespace MealTrace.Application.Dtos.Auth;

public sealed record LoginRequest(string? Email, string Password, string? Identifier = null);

namespace MealTrace.Application.Dtos.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

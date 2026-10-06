namespace MealTrace.Application.Dtos.Accounts;

public sealed record PasswordResetResponse(Guid UserId, string FullName, string? PhoneNumber, string? Email,
    string TemporaryPassword, string Message, bool IsActive);

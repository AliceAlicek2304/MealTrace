namespace MealTrace.Application.Dtos.Accounts;

public sealed record AccountCreatedResponse(AccountView User, string TemporaryPassword, string Message);

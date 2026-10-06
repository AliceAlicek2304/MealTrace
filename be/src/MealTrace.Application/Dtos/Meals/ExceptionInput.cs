namespace MealTrace.Application.Dtos.Meals;

public sealed record ExceptionInput(Guid StudentId, string Reason, string? Action = null, Guid? ExpectedEventId = null, bool? WillEat = null);

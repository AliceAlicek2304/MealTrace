namespace MealTrace.Application.Dtos.Students;

public sealed record ChangeEnrollment(Guid? ClassId, DateOnly EffectiveDate, string Reason, int Revision);

namespace MealTrace.Application.Models.Persistence;

public sealed record MealEnrollmentMember(Guid StudentId, string StudentCode, string FullName, Guid EnrollmentId,
    Guid ClassId, string ClassName, string SchoolYear, DateOnly? YearStart, DateOnly? YearEnd);

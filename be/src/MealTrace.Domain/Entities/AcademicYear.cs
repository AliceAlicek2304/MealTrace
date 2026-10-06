namespace MealTrace.Domain.Entities;

public sealed class AcademicYear
{
    public required string Code { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

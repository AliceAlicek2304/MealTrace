namespace MealTrace.Domain.Entities;

public sealed class MealCalendarException
{
    public required string SchoolYear { get; set; }
    public DateOnly Date { get; set; }
    public bool IsOpen { get; set; }
    public required string MealTypesJson { get; set; }
    public required string Reason { get; set; }
}

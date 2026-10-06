namespace MealTrace.Domain.Entities;

public sealed class MealSchedule
{
    public required string SchoolYear { get; set; }
    public int WeekdayMask { get; set; }
    public required string MealTypesJson { get; set; }
    public int Revision { get; set; }
}

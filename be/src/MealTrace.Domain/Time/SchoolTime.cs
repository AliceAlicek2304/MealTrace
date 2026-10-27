namespace MealTrace.Domain.Time;

/// <summary>School calendar uses UTC+7; persisted instants remain UTC.</summary>
public static class SchoolTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    public static readonly TimeOnly CutoffTime = new(7, 30);
    public static readonly TimeOnly BreakfastCutoffTime = new(21, 0);
    public static readonly TimeOnly DayCutoffTime = new(7, 0);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(Offset).DateTime);

    public static DateTimeOffset Cutoff(DateOnly date) => Cutoff(date, null);

    public static DateTimeOffset Cutoff(DateOnly date, string? mealType)
    {
        if (string.Equals(mealType, "Breakfast", StringComparison.OrdinalIgnoreCase) || mealType?.Contains("sáng", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new DateTimeOffset(date.AddDays(-1).ToDateTime(BreakfastCutoffTime), Offset).ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(mealType))
        {
            return new DateTimeOffset(date.ToDateTime(DayCutoffTime), Offset).ToUniversalTime();
        }

        return new DateTimeOffset(date.ToDateTime(CutoffTime), Offset).ToUniversalTime();
    }

    public static DateOnly EarliestEnrollmentDate(DateTimeOffset now)
    {
        var today = Today(now);
        return now >= Cutoff(today) ? today.AddDays(1) : today;
    }
}

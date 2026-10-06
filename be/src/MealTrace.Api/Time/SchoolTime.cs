namespace MealTrace.Api.Time;

/// <summary>School calendar uses UTC+7; persisted instants remain UTC.</summary>
public static class SchoolTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    public static readonly TimeOnly CutoffTime = new(7, 30);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(Offset).DateTime);

    public static DateTimeOffset Cutoff(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(CutoffTime), Offset).ToUniversalTime();

    public static DateOnly EarliestEnrollmentDate(DateTimeOffset now)
    {
        var today = Today(now);
        return now >= Cutoff(today) ? today.AddDays(1) : today;
    }
}

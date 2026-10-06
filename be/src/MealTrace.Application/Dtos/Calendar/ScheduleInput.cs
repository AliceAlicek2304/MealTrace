namespace MealTrace.Application.Dtos.Calendar;

public sealed record ScheduleInput(int[] Weekdays, string[] MealTypes, int ExpectedRevision, string Reason);

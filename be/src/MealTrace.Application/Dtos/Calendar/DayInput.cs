namespace MealTrace.Application.Dtos.Calendar;

public sealed record DayInput(string Mode, string[] MealTypes, int ExpectedRevision, string Reason);

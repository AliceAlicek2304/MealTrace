namespace MealTrace.Application.Exceptions;

public sealed class CalendarConflict(string message) : Exception(message);

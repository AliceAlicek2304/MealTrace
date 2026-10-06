namespace MealTrace.Application.Dtos.Calendar;

public sealed record GenerateInput(DateOnly From, DateOnly To, int ExpectedRevision, string? PreviewToken = null);

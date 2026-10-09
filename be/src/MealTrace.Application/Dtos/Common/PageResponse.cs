namespace MealTrace.Application.Dtos.Common;

public sealed record PageResponse<T>(List<T> Items, int Total, int Page, int PageSize);

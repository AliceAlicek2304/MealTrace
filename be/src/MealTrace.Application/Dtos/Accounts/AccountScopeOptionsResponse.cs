
namespace MealTrace.Application.Dtos.Accounts;

public sealed record AccountScopeOptionsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("classes")]
    public required List<ClassScopeOption> Classes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("students")]
    public required List<StudentScopeOption> Students { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("selectedClasses")]
    public required List<ClassScopeOption> SelectedClasses { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("selectedStudents")]
    public required List<StudentScopeOption> SelectedStudents { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("classTotal")]
    public int ClassTotal { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("studentTotal")]
    public int StudentTotal { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("classPage")]
    public int ClassPage { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("studentPage")]
    public int StudentPage { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

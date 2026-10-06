using MealTrace.Application.Dtos.Accounts;

namespace MealTrace.Application.Dtos.Responses;

public sealed record AccountListResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required AccountView[] Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

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

public sealed record ClassScopeOption
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record StudentScopeOption
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public required string Name { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("classId")]
    public Guid ClassId { get; init; }
}

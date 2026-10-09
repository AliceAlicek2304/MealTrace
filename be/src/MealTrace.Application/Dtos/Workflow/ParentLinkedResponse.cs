
namespace MealTrace.Application.Dtos.Workflow;

public sealed record ParentLinkedResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("parentId")]
    public Guid ParentId { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("studentId")]
    public Guid StudentId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public bool Created { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("temporaryPassword")]
    public string? TemporaryPassword { get; init; }
    public MealTrace.Application.Dtos.Notifications.NotificationResponse? Notification { get; init; }
}

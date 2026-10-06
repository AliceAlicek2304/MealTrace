
namespace MealTrace.Application.Dtos.Workflow;

public sealed record MealDayCreatedResponse
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public required string SchoolYear { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
}

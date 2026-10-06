
namespace MealTrace.Application.Dtos.Workflow;

public sealed record ClassCreatedResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string SchoolYear { get; init; }
}

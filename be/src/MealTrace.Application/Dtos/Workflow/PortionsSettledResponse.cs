
namespace MealTrace.Application.Dtos.Workflow;

public sealed record PortionsSettledResponse
{
    public Guid Id { get; init; }
    public int Classes { get; init; }
    public int Total { get; init; }
}

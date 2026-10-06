
namespace MealTrace.Application.Dtos.Workflow;

public sealed record StudentCreatedResponse
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
    public int Revision { get; init; }
}

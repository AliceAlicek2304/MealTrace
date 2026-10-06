namespace MealTrace.Application.Models.Persistence;

public sealed record SettlementStudentRow
{
    public Guid PortionSettlementId { get; init; } = default!;
    public Guid StudentId { get; init; } = default!;
    public string StudentName { get; init; } = default!;
}

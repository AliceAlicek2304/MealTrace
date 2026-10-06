
namespace MealTrace.Application.Dtos.Portions;

public sealed record SettlementDecisionSummary
{
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string StudentCode { get; init; }
    public bool WillEat { get; init; }
    public required string Source { get; init; }
    public Guid? EnrollmentId { get; init; }
    public Guid? AbsenceId { get; init; }
    public Guid? ExceptionId { get; init; }
    public Guid? AmendmentId { get; init; }
}

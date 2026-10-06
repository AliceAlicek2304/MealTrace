namespace MealTrace.Domain.Entities;

public sealed class SettlementDecision
{
    public Guid PortionSettlementId { get; set; }
    public PortionSettlement PortionSettlement { get; set; } = null!;
    public Guid StudentId { get; set; }
    public required string StudentName { get; set; }
    public required string StudentCode { get; set; }
    public bool WillEat { get; set; }
    public Guid? EnrollmentId { get; set; }
    public Guid? AbsenceId { get; set; }
    public Guid? ExceptionId { get; set; }
    public Guid? AmendmentId { get; set; }
    public required string Source { get; set; }
}


namespace MealTrace.Application.Dtos.Portions;

public sealed record PortionAmendmentSummary
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string StudentCode { get; init; }
    public Guid BaseSettlementId { get; init; }
    public int BaseVersion { get; init; }
    public Guid? EnrollmentId { get; init; }
    public bool WasEating { get; init; }
    public bool WillEat { get; init; }
    public int Quantity { get; init; }
    public bool IsQuantityOnly { get; init; }
    public List<AmendmentStudentSummary> Students { get; init; } = [];
    public required string Reason { get; init; }
    public required string RequestedByName { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public required string Status { get; init; }
    public string? ReviewReason { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public Guid? AppliedSettlementId { get; init; }
}

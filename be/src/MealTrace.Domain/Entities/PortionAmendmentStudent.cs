namespace MealTrace.Domain.Entities;

public sealed class PortionAmendmentStudent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public required string StudentName { get; set; }
    public required string StudentCode { get; set; }
    public Guid? EnrollmentId { get; set; }
}

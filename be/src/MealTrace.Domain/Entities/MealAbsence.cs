namespace MealTrace.Domain.Entities;

public sealed class MealAbsence
{
    public string? SchoolYear { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ReportedByUserId { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
}

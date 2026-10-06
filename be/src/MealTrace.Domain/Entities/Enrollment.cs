namespace MealTrace.Domain.Entities;

public sealed class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    // Exclusive: a child moves from A to B on D when A ends on D and B starts on D.
    public DateOnly? EndDate { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
    public string Reason { get; set; } = "Ghi danh ban đầu";
    public string? EndReason { get; set; }
    public Guid? EndedByUserId { get; set; }
    public DateTimeOffset? EndRecordedAt { get; set; }
}

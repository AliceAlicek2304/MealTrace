namespace MealTrace.Domain.Entities;

public sealed class MealCalendarAudit
{
    public required string ActorName { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SchoolYear { get; set; }
    public DateOnly? Date { get; set; }
    public required string Kind { get; set; }
    public required string BeforeJson { get; set; }
    public required string AfterJson { get; set; }
    public required string Reason { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

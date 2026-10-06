namespace MealTrace.Domain.Entities;

public sealed class MealRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    // null = restore the default decision (including any parent absence).
    public bool? WillEat { get; set; }
    public int Sequence { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public Guid? SupersedesId { get; set; }
}

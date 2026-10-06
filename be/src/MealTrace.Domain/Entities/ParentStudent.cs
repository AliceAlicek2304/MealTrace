namespace MealTrace.Domain.Entities;

public sealed class ParentStudent
{
    public Guid UserId { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
}

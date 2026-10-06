namespace MealTrace.Domain.Entities;

public sealed class TeacherAssignment
{
    public Guid UserId { get; set; }

    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
}

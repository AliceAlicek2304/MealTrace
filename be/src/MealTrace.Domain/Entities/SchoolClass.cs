namespace MealTrace.Domain.Entities;

public sealed class SchoolClass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string SchoolYear { get; set; }
    public List<Student> Students { get; set; } = [];
}

namespace MealTrace.Domain.Entities;

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StudentCode { get; set; } = "HS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    public required string FullName { get; set; }
    // Compatibility pointer to the latest enrolled class. Date-based workflows use Enrollments.
    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int Revision { get; set; }
    public List<Enrollment> Enrollments { get; set; } = [];
}

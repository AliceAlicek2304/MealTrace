using Microsoft.AspNetCore.Identity;

namespace MealTrace.Api.Data;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TeacherAssignment> TeacherAssignments { get; set; } = [];
    public List<ParentStudent> ParentStudents { get; set; } = [];
    public InspectorGrant? InspectorGrant { get; set; }
}

public sealed class TeacherAssignment
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
}

public sealed class ParentStudent
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
}

public sealed class InspectorGrant
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateOnly ExpiresOn { get; set; }
    public Guid GrantedById { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}

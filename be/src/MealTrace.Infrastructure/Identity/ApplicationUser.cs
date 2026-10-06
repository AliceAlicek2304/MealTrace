using MealTrace.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace MealTrace.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TeacherAssignment> TeacherAssignments { get; set; } = [];
    public List<ParentStudent> ParentStudents { get; set; } = [];
    public InspectorGrant? InspectorGrant { get; set; }
}

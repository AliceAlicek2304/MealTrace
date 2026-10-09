namespace MealTrace.Application.Dtos.Students;

// Explicit profile replacement allows clearing optional fields; older name-only clients preserve them.
public sealed record EditStudent(string FullName, int Revision, DateOnly? DateOfBirth = null, string? Gender = null, bool UpdateProfile = false);

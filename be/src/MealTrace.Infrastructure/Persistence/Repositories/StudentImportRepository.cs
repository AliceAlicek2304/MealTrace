using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class StudentImportRepository(MealTraceDbContext db) : IStudentImportRepository
{
    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct) =>
        PersistenceErrors.ExecuteAsync(() => db.Classes.AnyAsync(room => room.Id == classId, ct));
    public Task<List<string>> ExistingNamesAsync(Guid classId, CancellationToken ct) =>
        PersistenceErrors.ExecuteAsync(() => db.Students.AsNoTracking().Where(student => student.ClassId == classId ||
            student.Enrollments.Any(enrollment => enrollment.ClassId == classId)).Select(student => student.FullName).ToListAsync(ct));
    public void Add(Student student) => db.Students.Add(student);
}

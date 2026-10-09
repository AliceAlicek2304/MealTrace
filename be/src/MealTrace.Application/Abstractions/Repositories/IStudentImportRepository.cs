using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

public interface IStudentImportRepository
{
    Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct);
    Task<List<string>> ExistingNamesAsync(Guid classId, CancellationToken ct);
    void Add(Student student);
}

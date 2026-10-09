using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Application.Abstractions.Repositories;

public interface IStudentImportRepository
{
    Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct);
    Task<List<string>> ExistingNamesAsync(Guid classId, CancellationToken ct);
    void Add(Student student);
    void AddBatch(StudentImportBatch batch);
    Task<SchoolClass?> FindClassAsync(Guid classId, CancellationToken ct);
    Task<StudentImportHistory> HistoryAsync(Guid? classId, int page, int pageSize, CancellationToken ct);
    Task<StudentImportBatchDetail?> BatchAsync(Guid id, CancellationToken ct);
}

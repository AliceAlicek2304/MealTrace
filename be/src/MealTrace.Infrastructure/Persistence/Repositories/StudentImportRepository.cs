using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class StudentImportRepository(MealTraceDbContext db) : IStudentImportRepository
{
    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct) =>
        PersistenceErrors.ExecuteAsync(() => db.Classes.AnyAsync(room => room.Id == classId, ct));
    public Task<List<string>> ExistingNamesAsync(Guid classId, CancellationToken ct) =>
        PersistenceErrors.ExecuteAsync(() => db.Students.AsNoTracking().Where(student => student.ClassId == classId ||
            student.Enrollments.Any(enrollment => enrollment.ClassId == classId)).Select(student => student.FullName).ToListAsync(ct));
    public void Add(Student student) => db.Students.Add(student);
    public void AddBatch(StudentImportBatch batch) => db.StudentImportBatches.Add(batch);
    public Task<SchoolClass?> FindClassAsync(Guid classId, CancellationToken ct) => db.Classes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == classId, ct);
    private IQueryable<StudentImportBatchSummary> Summaries(IQueryable<StudentImportBatch> batches) =>
        from batch in batches
        join user in db.Users.AsNoTracking() on batch.ImportedByUserId equals user.Id
        select new StudentImportBatchSummary(batch.Id, batch.FileName, batch.SheetName, batch.ClassId,
            batch.ClassName, batch.SchoolYear, batch.StartDate, batch.Created, batch.ImportedAt, user.FullName);

    public async Task<StudentImportHistory> HistoryAsync(Guid? classId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.StudentImportBatches.AsNoTracking().AsQueryable();
        if (classId.HasValue) query = query.Where(x => x.ClassId == classId);
        var total = await query.CountAsync(ct);
        var items = await Summaries(query.OrderByDescending(x => x.ImportedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToArrayAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task<StudentImportBatchDetail?> BatchAsync(Guid id, CancellationToken ct)
    {
        var batch = await Summaries(db.StudentImportBatches.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        if (batch is null) return null;
        var rows = await db.Set<StudentImportBatchItem>().AsNoTracking().Where(x => x.BatchId == id)
            .OrderBy(x => x.SourceRow).Select(x => new StudentImportRow(x.SourceRow, x.FullName, null, x.DateOfBirth, x.Gender, null)).ToArrayAsync(ct);
        return new(batch, rows);
    }
}

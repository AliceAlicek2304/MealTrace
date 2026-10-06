using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class MealExceptionRepository(MealTraceDbContext db) : IMealExceptionRepository
{
    public async Task<MealDay?> FindMealDayAsync(Guid id, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        });
    }
    public async Task<MealDay?> FindHistoryMealDayAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        });
    }
    public async Task<int> CountStudentExceptionsAsync(Guid id, Guid studentId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == id && x.StudentId == studentId);
            return await query.CountAsync();
        });
    }
    public async Task<List<MealRegistration>> ListStudentExceptionsAsync(Guid id, Guid studentId, int number, int size)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == id && x.StudentId == studentId);
            return await query.OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Skip((number - 1) * size).Take(size).ToListAsync();
        });
    }
    public async Task<MealRegistration?> FindLatestExceptionAsync(Guid id, ExceptionInput input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == id && x.StudentId == input.StudentId).OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
        });
    }
    public async Task<string> GetActorNameAsync(Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).AsNoTracking().Where(x => x.Id == userId).Select(x => x.FullName).SingleAsync();
        });
    }
    public async Task<Guid[]> ListAssignedClassIdsAsync(CancellationToken ct, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.ClassId).ToArrayAsync(ct);
        });
    }
    public async Task<bool> HasTeacherAssignmentAsync(Guid classId, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AnyAsync(x => x.UserId == userId && x.ClassId == classId);
        });
    }
    public async Task<Enrollment?> FindEnrollmentAtCutoffAsync(MealDay day, Guid studentId, DateTimeOffset now)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await EnrollmentQueries.OnDate(db, day.Date).Include("Class").FirstOrDefaultAsync(x => x.StudentId == studentId && x.RecordedAt <= (now < day.CutoffAt ? now : day.CutoffAt));
        });
    }

    public void AddMealRegistration(MealRegistration value) => db.MealRegistrations.Add(value);
    public Task LockStudentAsync(Guid id, CancellationToken ct = default) => db.LockStudentAsync(id, ct);
    public Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct = default) => db.LockMealDayAsync(id, ct);
}

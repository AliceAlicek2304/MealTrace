using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Students;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class StudentAdministrationRepository(MealTraceDbContext db) : IStudentAdministrationRepository
{
    public async Task<int> CountClassesAsync(string? search)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Classes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
            }
            return await query.CountAsync();
        });
    }
    public async Task<List<ClassSummary>> SearchClassesAsync(string? search, int number, int size, DateOnly date)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Classes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
            }
            return await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size).Select(x => new ClassSummary
            {
                Id = x.Id,
                Name = x.Name,
                SchoolYear = x.SchoolYear,
                StudentCount = db.Enrollments.Count(e => e.ClassId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
            }).ToListAsync();
        });
    }
    public async Task<SchoolClass?> FindTrackedClassAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.FindAsync(id);
        });
    }
    public async Task<bool> ClassNameUsedByOtherClassAsync(Guid id, SchoolClass room, string? name)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.AnyAsync(x => x.Id != id && x.SchoolYear == room.SchoolYear && x.Name == name);
        });
    }
    public async Task<int> CountStudentsAsync(Guid? classId, DateOnly date, string? status, string? search, Guid? teacherId = null, string? parentStatus = null)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = StudentQuery(classId, date, status, search, teacherId, parentStatus);
            return await query.CountAsync();
        });
    }
    public async Task<List<StudentSummaryRow>> SearchStudentsAsync(Guid? classId, DateOnly date, string? status, string? search, int number, int size, Guid? teacherId = null, string? parentStatus = null)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = StudentQuery(classId, date, status, search, teacherId, parentStatus);
            return await query.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size).Select(x => new StudentSummaryRow
            {
                Id = x.Id,
                StudentCode = x.StudentCode,
                FullName = x.FullName,
                DateOfBirth = x.DateOfBirth,
                Gender = x.Gender,
                Revision = x.Revision,
                IsActive = db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)),
                ClassId = db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)).Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? x.ClassId,
                ClassName = db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)).Select(e => e.Class.Name).FirstOrDefault() ?? x.Class.Name
            }).ToListAsync();
        });
    }
    private IQueryable<Student> StudentQuery(Guid? classId, DateOnly date, string? status, string? search, Guid? teacherId, string? parentStatus)
    {
        var query = db.Students.AsNoTracking();
        if (classId.HasValue)
            query = query.Where(x => (db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
                .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? x.ClassId) == classId);
        if (teacherId.HasValue)
            query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == teacherId
                && a.ClassId == (db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
                    .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? x.ClassId)));
        if (status == "ACTIVE")
            query = query.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
        else if (status == "INACTIVE")
            query = query.Where(x => !db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
        if (parentStatus == "LINKED") query = query.Where(x => db.ParentStudents.Any(p => p.StudentId == x.Id));
        else if (parentStatus == "UNLINKED") query = query.Where(x => !db.ParentStudents.Any(p => p.StudentId == x.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
        }
        return query;
    }
    public async Task<List<ParentStudentRow>> ListStudentParentsAsync(Guid[] ids)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AsNoTracking().Where(x => ids.Contains(x.StudentId)).Join(db.Users.Select(IdentityService.AccountProjection), link => link.UserId, user => user.Id, (link, user) => new ParentStudentRow
            {
                StudentId = link.StudentId,
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            }).ToListAsync();
        });
    }
    public async Task<Student?> FindTrackedStudentAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.FindAsync(id);
        });
    }
    public async Task<bool> StudentExistsAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.AnyAsync(x => x.Id == id);
        });
    }
    public async Task<List<EnrollmentHistoryItem>> ListEnrollmentHistoryAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Enrollments.AsNoTracking().Where(x => x.StudentId == id).OrderBy(x => x.StartDate).Select(x => new EnrollmentHistoryItem
            {
                Id = x.Id,
                ClassId = x.ClassId,
                ClassName = x.Class.Name,
                SchoolYear = x.Class.SchoolYear,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Reason = x.Reason,
                EndReason = x.EndReason,
                RecordedAt = x.RecordedAt,
                RecordedByUserId = x.RecordedByUserId,
                EndRecordedAt = x.EndRecordedAt,
                EndedByUserId = x.EndedByUserId
            }).ToListAsync();
        });
    }
    public async Task<Student?> FindStudentWithEnrollmentsAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.Include("Enrollments").FirstOrDefaultAsync(x => x.Id == id);
        });
    }
    public async Task<bool> ClassExistsAsync(ChangeEnrollment input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.AnyAsync(x => x.Id == input.ClassId);
        });
    }

    public void AddEnrollment(Enrollment value) => db.Enrollments.Add(value);
    public Task LockStudentAsync(Guid id, CancellationToken ct = default) => db.LockStudentAsync(id, ct);
}

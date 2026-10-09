using System.Text;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Students;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class ParentLinkRepository(MealTraceDbContext db) : IParentLinkRepository
{
    private IQueryable<Student> StudentsInClass(Guid classId, DateOnly date) => db.Students.Where(student =>
        (db.Enrollments.Where(e => e.StudentId == student.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
            .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? student.ClassId) == classId);
    public Task<List<Student>> FindClassStudentsByNameAsync(Guid classId, DateOnly date, string name) => PersistenceErrors.ExecuteAsync(async () =>
    {
        // Vietnamese case/spacing normalization must be consistent across supported database providers.
        var rows = await StudentsInClass(classId, date).Where(x => x.IsActive).OrderBy(x => x.Id).Take(1001).ToListAsync();
        if (rows.Count > 1000) return [];
        static string Key(string value) => string.Join(' ', value.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        return rows.Where(x => Key(x.FullName) == Key(name)).Take(2).ToList();
    });
    public Task<Guid?> CurrentClassAsync(Guid studentId, DateOnly date) => PersistenceErrors.ExecuteAsync(() => db.Students.Where(s => s.Id == studentId)
        .Select(s => db.Enrollments.Where(e => e.StudentId == s.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
            .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? s.ClassId).Select(x => (Guid?)x).SingleOrDefaultAsync());
    public Task<List<ParentLinkClass>> ClassesAsync(Guid? teacherId) => PersistenceErrors.ExecuteAsync(async () =>
    {
        var query = db.Classes.AsNoTracking();
        if (teacherId.HasValue) query = query.Where(c => db.TeacherAssignments.Any(a => a.UserId == teacherId && a.ClassId == c.Id));
        var rows = await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.SchoolYear }).ToListAsync();
        return rows.Select(x => new ParentLinkClass(x.Id, x.Name, x.SchoolYear)).ToList();
    });
    public async Task UnlinkAsync(Guid parentId, Guid studentId)
    {
        var link = await PersistenceErrors.ExecuteAsync(() => db.ParentStudents.SingleOrDefaultAsync(x => x.UserId == parentId && x.StudentId == studentId));
        if (link is not null) db.ParentStudents.Remove(link);
    }
    public Task<Student?> FindStudentByCodeAsync(string code) => PersistenceErrors.ExecuteAsync(() => db.Students.SingleOrDefaultAsync(x => x.StudentCode == code));
    public Task<Student?> FindStudentAsync(Guid id) => PersistenceErrors.ExecuteAsync(() => db.Students.SingleOrDefaultAsync(x => x.Id == id));
    public Task<ParentLinkRequest?> FindAsync(Guid id) => PersistenceErrors.ExecuteAsync(() => db.ParentLinkRequests.SingleOrDefaultAsync(x => x.Id == id));
    public Task<bool> LinkedAsync(Guid parentId, Guid studentId) => PersistenceErrors.ExecuteAsync(() => db.ParentStudents.AnyAsync(x => x.UserId == parentId && x.StudentId == studentId));
    public Task<bool> PendingAsync(Guid parentId, Guid studentId) => PersistenceErrors.ExecuteAsync(() => db.ParentLinkRequests.AnyAsync(x => x.ParentId == parentId && x.StudentId == studentId && x.Status == "PENDING"));
    public Task<int> PendingCountAsync(Guid parentId) => PersistenceErrors.ExecuteAsync(() => db.ParentLinkRequests.CountAsync(x => x.ParentId == parentId && x.Status == "PENDING"));
    public void Add(ParentLinkRequest request) => db.ParentLinkRequests.Add(request);
    public void Link(Guid parentId, Guid studentId) => db.ParentStudents.Add(new ParentStudent { UserId = parentId, StudentId = studentId });
    public Task<bool> TeacherCanManageAsync(Guid teacherId, Guid studentId, DateOnly date) => PersistenceErrors.ExecuteAsync(() =>
        ScopedStudents(teacherId, date).AnyAsync(x => x.Id == studentId));
    private IQueryable<Student> ScopedStudents(Guid teacherId, DateOnly date) => db.Students.Where(student =>
        db.TeacherAssignments.Any(assignment => assignment.UserId == teacherId && assignment.ClassId ==
            (db.Enrollments.Where(e => e.StudentId == student.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
                .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? student.ClassId)));

    public Task<ParentLinkRequestPage> ListAsync(Guid? parentId, Guid? teacherId, DateOnly date, string? status, int page, int size, Guid? classId = null, string? schoolYear = null) => PersistenceErrors.ExecuteAsync(async () =>
    {
        var query = db.ParentLinkRequests.AsNoTracking();
        if (parentId.HasValue) query = query.Where(x => x.ParentId == parentId);
        if (teacherId.HasValue)
        {
            var scopedIds = ScopedStudents(teacherId.Value, date).Select(x => x.Id);
            query = query.Where(x => scopedIds.Contains(x.StudentId));
        }
        if (classId.HasValue)
        {
            var classStudents = StudentsInClass(classId.Value, date).Select(x => x.Id);
            query = query.Where(x => classStudents.Contains(x.StudentId));
        }
        if (!string.IsNullOrWhiteSpace(schoolYear))
        {
            var rooms = db.Classes.Where(c => c.SchoolYear == schoolYear).Select(c => c.Id);
            var studentIds = db.Students.Where(s => rooms.Contains(db.Enrollments.Where(e => e.StudentId == s.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
                .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? s.ClassId)).Select(s => s.Id);
            query = query.Where(x => studentIds.Contains(x.StudentId));
        }
        if (status is not null) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Join(db.Users, request => request.ParentId, user => user.Id, (request, user) => new { request, user })
            .Select(x => new ParentLinkRequestView(x.request.Id, x.request.StudentCode, x.request.StudentName, x.request.Relationship, x.request.Note,
                x.request.Status, x.request.RequestedAt, x.request.ReviewedAt, x.request.ReviewReason, x.request.Revision,
                x.user.FullName, x.user.PhoneNumber,
                parentId.HasValue ? null : db.Enrollments.Where(e => e.StudentId == x.request.StudentId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
                    .Select(e => e.Class.Name).FirstOrDefault() ?? db.Students.Where(s => s.Id == x.request.StudentId).Select(s => s.Class.Name).FirstOrDefault(),
                parentId.HasValue ? null : db.Enrollments.Where(e => e.StudentId == x.request.StudentId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
                    .Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? db.Students.Where(s => s.Id == x.request.StudentId).Select(s => (Guid?)s.ClassId).FirstOrDefault(),
                x.request.RevokedAt, x.request.RevocationReason))
            .ToListAsync();
        return new ParentLinkRequestPage(items, total, page, size);
    });
}

using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Accounts;
using System.Data;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(MealTraceDbContext db) : IAccountRepository
{
    public async Task<int> CountAccountsAsync(Guid? classId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Users.Select(IdentityService.AccountProjection).AsNoTracking().AsQueryable();
            if (classId is not null)
                query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == x.Id && a.ClassId == classId));
            return await query.CountAsync();
        });
    }
    public async Task<List<IdentityAccount>> ListAccountsAsync(Guid? classId, int number, int size)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Users.Select(IdentityService.AccountProjection).AsNoTracking().AsQueryable();
            if (classId is not null)
                query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == x.Id && a.ClassId == classId));
            return await query.OrderBy(x => x.Email).Skip((number - 1) * size).Take(size).ToListAsync();
        });
    }
    public async Task<List<AccountRoleRow>> ListAccountRolesAsync(Guid[] ids)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await (
                from link in db.UserRoles.Select(link => new AccountRoleLink
                {
                    UserId = link.UserId,
                    RoleId = link.RoleId
                }).AsNoTracking()
                join role in db.Roles.Select(role => new AccountRole
                {
                    Id = role.Id,
                    Name = role.Name
                }).AsNoTracking() on link.RoleId equals role.Id
                where ids.Contains(link.UserId)
                select new AccountRoleRow
                {
                    UserId = link.UserId,
                    Role = role.Name!
                }

            ).ToListAsync();
        });
    }
    public async Task<List<TeacherAssignment>> ListTeacherAssignmentsAsync(Guid[] ids)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync();
        });
    }
    public async Task<List<ParentStudent>> ListParentLinksAsync(Guid[] ids)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync();
        });
    }
    public async Task<List<InspectorGrant>> ListInspectorGrantsAsync(Guid[] ids)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.InspectorGrants.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync();
        });
    }
    public async Task<IdentityAccount?> FindAccountAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        });
    }
    public async Task<Guid[]> ListAssignedClassIdsAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AsNoTracking().Where(x => x.UserId == id).Select(x => x.ClassId).ToArrayAsync();
        });
    }
    public async Task<Guid[]> ListLinkedStudentIdsAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AsNoTracking().Where(x => x.UserId == id).Select(x => x.StudentId).ToArrayAsync();
        });
    }
    public async Task<InspectorGrant?> FindInspectorGrantAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.InspectorGrants.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id);
        });
    }
    public async Task<List<ClassScopeOption>> SearchClassOptionsAsync(string? search, Guid? classId, DateOnly date, int cp, int size)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var classes = db.Classes.AsNoTracking();
            var students = db.Students.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                classes = classes.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
                students = students.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
            }
            if (classId.HasValue)
                students = students.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
            return await classes.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((cp - 1) * size).Take(size).Select(x => new ClassScopeOption
            {
                Id = x.Id,
                Name = x.Name + " · " + x.SchoolYear
            }).ToListAsync();
        });
    }
    public async Task<List<StudentScopeOption>> SearchStudentOptionsAsync(string? search, Guid? classId, DateOnly date, int sp, int size)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var classes = db.Classes.AsNoTracking();
            var students = db.Students.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                classes = classes.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
                students = students.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
            }
            if (classId.HasValue)
                students = students.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
            return await students.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((sp - 1) * size).Take(size).Select(x => new StudentScopeOption
            {
                Id = x.Id,
                Name = x.StudentCode + " · " + x.FullName,
                ClassId = x.ClassId
            }).ToListAsync();
        });
    }
    public async Task<List<ClassScopeOption>> ListSelectedClassOptionsAsync(Guid[] classIds)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.Where(x => classIds.Contains(x.Id)).Select(x => new ClassScopeOption
            {
                Id = x.Id,
                Name = x.Name + " · " + x.SchoolYear
            }).ToListAsync();
        });
    }
    public async Task<List<StudentScopeOption>> ListSelectedStudentOptionsAsync(Guid[] studentIds)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.Where(x => studentIds.Contains(x.Id)).Select(x => new StudentScopeOption
            {
                Id = x.Id,
                Name = x.StudentCode + " · " + x.FullName,
                ClassId = x.ClassId
            }).ToListAsync();
        });
    }
    public async Task<int> CountClassOptionsAsync(string? search, Guid? classId, DateOnly date)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var classes = db.Classes.AsNoTracking();
            var students = db.Students.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                classes = classes.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
                students = students.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
            }
            if (classId.HasValue)
                students = students.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
            return await classes.CountAsync();
        });
    }
    public async Task<int> CountStudentOptionsAsync(string? search, Guid? classId, DateOnly date)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var classes = db.Classes.AsNoTracking();
            var students = db.Students.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                classes = classes.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
                students = students.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
            }
            if (classId.HasValue)
                students = students.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
            return await students.CountAsync();
        });
    }
    public async Task<bool> PhoneExistsAsync(string? phone)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).AnyAsync(x => x.PhoneNumber == phone);
        });
    }
    public async Task<bool> PhoneUsedByOtherAccountAsync(string? phone, Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).AnyAsync(x => x.PhoneNumber == phone && x.Id != id);
        });
    }
    public async Task<int> CountExistingClassesAsync(AccountInput input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.CountAsync(x => input.ClassIds.Contains(x.Id));
        });
    }
    public async Task<int> CountExistingStudentsAsync(AccountInput input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.CountAsync(x => input.StudentIds.Contains(x.Id));
        });
    }
    public async Task<int> DeleteTeacherAssignmentsAsync(Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        });
    }
    public async Task<int> DeleteParentLinksAsync(Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        });
    }
    public async Task<InspectorGrant?> FindTrackedInspectorGrantAsync(Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.InspectorGrants.FindAsync(userId);
        });
    }

    public void AddTeacherAssignments(IEnumerable<TeacherAssignment> value) => db.TeacherAssignments.AddRange(value);
    public void AddParentStudents(IEnumerable<ParentStudent> value) => db.ParentStudents.AddRange(value);
    public void AddInspectorGrant(InspectorGrant value) => db.InspectorGrants.Add(value);
    public void RemoveInspectorGrant(InspectorGrant value) => db.InspectorGrants.Remove(value);
    public void AddAccountPasswordResetAudit(AccountPasswordResetAudit value) => db.AccountPasswordResetAudits.Add(value);
}

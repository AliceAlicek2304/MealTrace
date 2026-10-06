using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Workflow;
using System.Data;
using MealTrace.Domain.Time;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class WorkflowRepository(MealTraceDbContext db) : IWorkflowRepository
{
    public async Task<List<AcademicYearResponse>> ListAcademicYearsAsync()
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.AsNoTracking().OrderBy(x => x.Code).Select(x => new AcademicYearResponse(x.Code, x.StartDate, x.EndDate)).ToListAsync();
        });
    }
    public async Task<List<string>> ListSchoolYearCodesAsync()
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.Select(x => x.SchoolYear).Union(db.AcademicYears.Select(x => x.Code)).OrderBy(x => x).ToListAsync();
        });
    }
    public async Task<Dictionary<string, AcademicYear>> GetAcademicYearsByCodeAsync()
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.ToDictionaryAsync(x => x.Code);
        });
    }
    public async Task<AcademicYear?> FindAcademicYearAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
        });
    }
    public async Task<bool> AcademicYearExistsAsync(string? nextCode)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.AnyAsync(x => x.Code == nextCode);
        });
    }
    public async Task<bool> SourceAcademicYearExistsAsync(YearDates input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.AnyAsync(x => x.Code == input.SourceYearCode);
        });
    }
    public async Task<AcademicYear?> FindTrackedAcademicYearAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.FindAsync(code);
        });
    }
    public async Task<List<ClassSummary>> ListAccessibleClassesAsync(DateOnly date, bool isAdmin, bool isTeacher, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Classes.AsNoTracking().AsQueryable();
            if (!isAdmin && isTeacher)
            {
                query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == userId && a.ClassId == x.Id));
            }
            return await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).Select(x => new ClassSummary
            {
                Id = x.Id,
                Name = x.Name,
                SchoolYear = x.SchoolYear,
                StudentCount = db.Enrollments.Count(s => s.ClassId == x.Id && s.StartDate <= date && (s.EndDate == null || s.EndDate > date))
            }).ToListAsync();
        });
    }
    public async Task<bool> ClassNameExistsAsync(string? name, string? year)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.AnyAsync(x => x.Name == name && x.SchoolYear == year);
        });
    }
    public async Task<List<ClassStudentRow>> ListClassStudentsAsync(Guid classId, DateTimeOffset now)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await EnrollmentQueries.OnDate(db, SchoolTime.Today(now)).Where(x => x.ClassId == classId).OrderBy(x => x.Student.FullName).Select(x => new ClassStudentRow
            {
                Id = x.StudentId,
                StudentCode = x.Student.StudentCode,
                FullName = x.Student.FullName,
                ClassId = x.ClassId
            }).ToListAsync();
        });
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
    public async Task<bool> ClassExistsAsync(CreateStudent input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.AnyAsync(x => x.Id == input.ClassId);
        });
    }
    public async Task<bool> StudentCodeExistsAsync(Student student)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.AnyAsync(x => x.StudentCode == student.StudentCode);
        });
    }
    public async Task<bool> StudentExistsAsync(Guid studentId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.AnyAsync(x => x.Id == studentId);
        });
    }
    public async Task<IdentityAccount?> FindParentByPhoneAsync(string? phone)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).SingleOrDefaultAsync(x => x.PhoneNumber == phone);
        });
    }
    public async Task<bool> ParentLinkExistsAsync(IdentityAccount parent, Guid studentId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AnyAsync(x => x.UserId == parent.Id && x.StudentId == studentId);
        });
    }
    public async Task<List<ParentChildSummary>> ListParentChildrenAsync(DateTimeOffset now, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await EnrollmentQueries.OnDate(db, SchoolTime.Today(now)).Where(x => db.ParentStudents.Any(p => p.UserId == userId && p.StudentId == x.StudentId)).OrderBy(x => x.Student.FullName).Select(x => new ParentChildSummary
            {
                StudentId = x.StudentId,
                StudentCode = x.Student.StudentCode,
                FullName = x.Student.FullName,
                ClassId = x.ClassId,
                ClassName = x.Class.Name,
                SchoolYear = x.Class.SchoolYear,
                YearStartDate = db.AcademicYears.Where(y => y.Code == x.Class.SchoolYear).Select(y => (DateOnly?)y.StartDate).FirstOrDefault(),
                YearEndDate = db.AcademicYears.Where(y => y.Code == x.Class.SchoolYear).Select(y => (DateOnly?)y.EndDate).FirstOrDefault()
            }).ToListAsync();
        });
    }
    public async Task<bool> ParentCanAccessStudentAsync(Guid userId, ReportAbsence input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AnyAsync(x => x.UserId == userId && x.StudentId == input.StudentId);
        });
    }
    public async Task<bool> HasActiveEnrollmentAsync(DateOnly today, ReportAbsence input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await EnrollmentQueries.OnDate(db, today).AnyAsync(x => x.StudentId == input.StudentId);
        });
    }
    public async Task<bool> HasOverlappingAbsenceAsync(ReportAbsence input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.AnyAsync(x => x.StudentId == input.StudentId && x.CancelledAt == null && x.FromDate <= input.ToDate && x.ToDate >= input.FromDate);
        });
    }
    public async Task<MealAbsence?> FindReportedAbsenceAsync(Guid id, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ReportedByUserId == userId);
        });
    }
    public async Task<MealAbsence> GetTrackedAbsenceAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.SingleAsync(x => x.Id == id);
        });
    }
    public async Task<bool> HasGuardianLinkAsync(Guid userId, MealAbsence absence)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AnyAsync(x => x.UserId == userId && x.StudentId == absence.StudentId);
        });
    }
    public async Task<bool> HasOtherOverlappingAbsenceAsync(Guid id, MealAbsence absence, ReportAbsence input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.AnyAsync(x => x.Id != id && x.StudentId == absence.StudentId && x.CancelledAt == null && x.FromDate <= input.ToDate && x.ToDate >= input.FromDate);
        });
    }
    public async Task<List<AbsenceSummary>> ListReportedAbsencesAsync(Guid reportedByUserId, Guid guardianUserId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.AsNoTracking().Where(x => x.ReportedByUserId == reportedByUserId && db.ParentStudents.Any(p => p.UserId == guardianUserId && p.StudentId == x.StudentId)).OrderByDescending(x => x.ReportedAt).Take(100).Select(x => new AbsenceSummary
            {
                Id = x.Id,
                StudentId = x.StudentId,
                StudentName = x.Student.FullName,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                Reason = x.Reason,
                ReportedAt = x.ReportedAt,
                CancelledAt = x.CancelledAt,
                SchoolYear = x.SchoolYear ?? db.Enrollments.Where(e => e.StudentId == x.StudentId && e.StartDate <= x.FromDate && (e.EndDate == null || e.EndDate > x.FromDate)).Select(e => e.Class.SchoolYear).FirstOrDefault()
            }).ToListAsync();
        });
    }
    public async Task<MealAbsence?> FindAbsenceForCancellationAsync(Guid id, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ReportedByUserId == userId);
        });
    }
    public async Task<MealAbsence> GetAbsenceForCancellationAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealAbsences.SingleAsync(x => x.Id == id);
        });
    }
    public async Task<bool> HasGuardianLinkForCancellationAsync(MealAbsence absence, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ParentStudents.AnyAsync(x => x.UserId == userId && x.StudentId == absence.StudentId);
        });
    }
    public async Task<bool> SchoolYearHasClassesAsync(string? schoolYear)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Classes.AnyAsync(x => x.SchoolYear == schoolYear);
        });
    }
    public async Task<bool> MealSessionExistsAsync(CreateMealDay input, string? mealType)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AnyAsync(x => x.Date == input.Date && x.MealType == mealType);
        });
    }
    public async Task<int> CountMealDaysAsync(DateOnly? date)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.MealDays.AsNoTracking().Where(x => date == null || x.Date == date);
            return await query.CountAsync();
        });
    }
    public async Task<List<WorkflowMealDaySummary>> ListMealDaysAsync(DateOnly? date, int number)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.MealDays.AsNoTracking().Where(x => date == null || x.Date == date);
            return await query.OrderByDescending(x => x.Date).ThenBy(x => x.MealType).ThenBy(x => x.Id).Skip((number - 1) * 25).Take(25).Select(x => new WorkflowMealDaySummary
            {
                Id = x.Id,
                Date = x.Date,
                MealType = x.MealType,
                SchoolYear = x.SchoolYear,
                CutoffAt = x.CutoffAt,
                IsCancelled = x.IsCancelled,
                CancellationReason = x.CancellationReason,
                IsSettled = x.SettledAt != null
            }).ToListAsync();
        });
    }
    public async Task<MealDay?> FindMealDayAsync(Guid id, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        });
    }
    public async Task<Guid[]> ListAssignedClassIdsAsync(CancellationToken ct, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.ClassId).ToArrayAsync(ct);
        });
    }
    public async Task<bool> HasSettlementAsync(Guid id, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionSettlements.AnyAsync(x => x.MealDayId == id, ct);
        });
    }
    public async Task<string?> FindEnrollmentYearCodeAsync(DateOnly from, Guid studentId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await EnrollmentQueries.OnDate(db, from).Where(x => x.StudentId == studentId).Select(x => x.Class.SchoolYear).FirstOrDefaultAsync();
        });
    }
    public async Task<AcademicYear?> FindAcademicYearContainingPeriodAsync(string? code, DateOnly from, DateOnly to)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code && x.StartDate <= from && x.EndDate >= to);
        });
    }
    public async Task<bool> HasTeacherAssignmentAsync(Guid classId, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AnyAsync(x => x.UserId == userId && x.ClassId == classId);
        });
    }

    public void AddAcademicYear(AcademicYear value) => db.AcademicYears.Add(value);
    public void AddMealAbsence(MealAbsence value) => db.MealAbsences.Add(value);
    public void AddSchoolClass(SchoolClass value) => db.Classes.Add(value);
    public void AddParentStudent(ParentStudent value) => db.ParentStudents.Add(value);
    public void AddPortionSettlements(IEnumerable<PortionSettlement> value) => db.PortionSettlements.AddRange(value);
    public void AddMealDay(MealDay value) => db.MealDays.Add(value);
    public void AddStudent(Student value) => db.Students.Add(value);
    public Task LockStudentAsync(Guid id, CancellationToken ct = default) => db.LockStudentAsync(id, ct);
    public Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct = default) => db.LockMealDayAsync(id, ct);
}

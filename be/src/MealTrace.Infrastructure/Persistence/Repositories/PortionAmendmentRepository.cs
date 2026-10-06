using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Portions;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class PortionAmendmentRepository(MealTraceDbContext db) : IPortionAmendmentRepository
{
    public async Task<bool> HasTeacherAssignmentAsync(Guid classId, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AnyAsync(x => x.UserId == userId && x.ClassId == classId);
        });
    }
    public async Task<string> GetActorNameAsync(Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).Where(x => x.Id == userId).Select(x => x.FullName).SingleAsync();
        });
    }
    public async Task<PortionSettlement?> FindLatestSettlementAsync(Guid dayId, Guid classId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionSettlements.Where(x => x.MealDayId == dayId && x.ClassId == classId).OrderByDescending(x => x.Version).Include("Students").Include("Decisions").AsSplitQuery().FirstOrDefaultAsync();
        });
    }
    public async Task<bool> HasPortionInAnotherClassAsync(Guid studentId, Guid dayId, Guid classId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.SettlementStudents.AnyAsync(x => x.StudentId == studentId && x.PortionSettlement.MealDayId == dayId && x.PortionSettlement.ClassId != null && x.PortionSettlement.ClassId != classId && !db.PortionSettlements.Any(newer => newer.MealDayId == dayId && newer.ClassId == x.PortionSettlement.ClassId && newer.Version > x.PortionSettlement.Version));
        });
    }
    public async Task<MealDay?> FindMealDayAsync(Guid dayId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dayId);
        });
    }
    public async Task<PortionSettlement> GetOriginalSettlementAsync(Guid dayId, Guid classId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionSettlements.AsNoTracking().Where(x => x.MealDayId == dayId && x.ClassId == classId).OrderBy(x => x.Version).Include("Students").Include("Decisions").AsSplitQuery().FirstAsync();
        });
    }
    public async Task<int> CountAmendmentCandidatesAsync(MealDay day, Guid classId, PortionSettlement current, string? term)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var members = EnrollmentQueries.OnDate(db, day.Date).Where(x => x.ClassId == classId);
            var candidatesQuery = db.Students.AsNoTracking().Where(x => members.Any(e => e.StudentId == x.Id) || db.SettlementStudents.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id) || db.SettlementDecisions.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id));
            if (!string.IsNullOrEmpty(term))
                candidatesQuery = candidatesQuery.Where(x => x.FullName.ToLower().Contains(term.ToLower()) || x.StudentCode.ToLower().Contains(term.ToLower()));
            return await candidatesQuery.CountAsync();
        });
    }
    public async Task<List<AmendmentCandidateRow>> SearchAmendmentCandidatesAsync(MealDay day, Guid classId, PortionSettlement current, string? term, int candidateNumber)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var members = EnrollmentQueries.OnDate(db, day.Date).Where(x => x.ClassId == classId);
            var candidatesQuery = db.Students.AsNoTracking().Where(x => members.Any(e => e.StudentId == x.Id) || db.SettlementStudents.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id) || db.SettlementDecisions.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id));
            if (!string.IsNullOrEmpty(term))
                candidatesQuery = candidatesQuery.Where(x => x.FullName.ToLower().Contains(term.ToLower()) || x.StudentCode.ToLower().Contains(term.ToLower()));
            return await candidatesQuery.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((candidateNumber - 1) * 25).Take(25).Select(x => new AmendmentCandidateRow
            {
                Id = x.Id,
                FullName = x.FullName,
                StudentCode = x.StudentCode
            }).ToListAsync();
        });
    }
    public async Task<int> CountAmendmentsAsync(Guid dayId, Guid classId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var requests = db.PortionAmendments.AsNoTracking().Where(x => x.BaseSettlement.MealDayId == dayId && x.BaseSettlement.ClassId == classId);
            return await requests.CountAsync();
        });
    }
    public async Task<List<PortionAmendmentSummary>> ListAmendmentsAsync(Guid dayId, Guid classId, int number)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var requests = db.PortionAmendments.AsNoTracking().Where(x => x.BaseSettlement.MealDayId == dayId && x.BaseSettlement.ClassId == classId);
            return await requests.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Skip((number - 1) * 25).Take(25).Select(x => new PortionAmendmentSummary
            {
                Id = x.Id,
                StudentId = x.StudentId,
                StudentName = x.StudentName,
                StudentCode = x.StudentCode,
                BaseSettlementId = x.BaseSettlementId,
                BaseVersion = x.BaseSettlement.Version,
                EnrollmentId = x.EnrollmentId,
                WasEating = x.WasEating,
                WillEat = x.WillEat,
                Reason = x.Reason,
                RequestedByName = x.RequestedByName,
                RequestedAt = x.RequestedAt,
                Status = x.Resolution == null ? "PENDING" : x.Resolution.Approved ? "APPROVED" : "REJECTED",
                ReviewReason = x.Resolution == null ? null : x.Resolution.Reason,
                ReviewedByName = x.Resolution == null ? null : x.Resolution.ReviewedByName,
                ReviewedAt = x.Resolution == null ? (DateTimeOffset?)null : x.Resolution.ReviewedAt,
                AppliedSettlementId = x.Resolution == null ? null : x.Resolution.AppliedSettlementId
            }).ToListAsync();
        });
    }
    public async Task<bool> TeacherCanRequestAmendmentAsync(RequestInput input, Guid userId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.TeacherAssignments.AnyAsync(x => x.UserId == userId && x.ClassId == input.ClassId);
        });
    }
    public async Task<Enrollment?> FindCandidateEnrollmentAsync(MealDay day, RequestInput input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await EnrollmentQueries.OnDate(db, day.Date).AsNoTracking().Where(x => x.ClassId == input.ClassId && x.StudentId == input.StudentId).Include("Student").FirstOrDefaultAsync();
        });
    }
    public async Task<bool> HasPendingAmendmentAsync(PortionSettlement current, RequestInput input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionAmendments.AnyAsync(x => x.BaseSettlementId == current.Id && x.StudentId == input.StudentId && x.Resolution == null);
        });
    }
    public async Task<string> GetStudentCodeAsync(RequestInput input)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Students.Where(x => x.Id == input.StudentId).Select(x => x.StudentCode).SingleAsync();
        });
    }
    public async Task<PortionAmendment?> FindAmendmentDetailsAsync(Guid requestId, Guid dayId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionAmendments.AsNoTracking().Include("BaseSettlement.Students").Include("BaseSettlement.Decisions").Include("Resolution.AppliedSettlement.Students").Include("Resolution.AppliedSettlement.Decisions").AsSplitQuery().FirstOrDefaultAsync(x => x.Id == requestId && x.BaseSettlement.MealDayId == dayId);
        });
    }
    public async Task<PortionAmendment?> FindTrackedAmendmentAsync(Guid requestId, Guid dayId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionAmendments.Include("BaseSettlement").Include("Resolution").FirstOrDefaultAsync(x => x.Id == requestId && x.BaseSettlement.MealDayId == dayId);
        });
    }

    public void AddPortionAmendmentResolution(PortionAmendmentResolution value) => db.PortionAmendmentResolutions.Add(value);
    public void AddPortionSettlement(PortionSettlement value) => db.PortionSettlements.Add(value);
    public void AddPortionAmendment(PortionAmendment value) => db.PortionAmendments.Add(value);
    public Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct = default) => db.LockMealDayAsync(id, ct);
}

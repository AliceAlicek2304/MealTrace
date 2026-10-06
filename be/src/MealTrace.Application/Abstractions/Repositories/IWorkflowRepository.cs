using MealTrace.Application.Dtos.Workflow;
using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IWorkflowRepository
{
    Task<List<AcademicYearResponse>> ListAcademicYearsAsync();
    Task<List<string>> ListSchoolYearCodesAsync();
    Task<Dictionary<string, AcademicYear>> GetAcademicYearsByCodeAsync();
    Task<AcademicYear?> FindAcademicYearAsync(string code);
    Task<bool> AcademicYearExistsAsync(string? nextCode);
    Task<bool> SourceAcademicYearExistsAsync(YearDates input);
    Task<AcademicYear?> FindTrackedAcademicYearAsync(string code);
    Task<List<ClassSummary>> ListAccessibleClassesAsync(DateOnly date, bool isAdmin, bool isTeacher, Guid userId);
    Task<bool> ClassNameExistsAsync(string? name, string? year);
    Task<List<ClassStudentRow>> ListClassStudentsAsync(Guid classId, DateTimeOffset now);
    Task<List<ParentStudentRow>> ListStudentParentsAsync(Guid[] ids);
    Task<bool> ClassExistsAsync(CreateStudent input);
    Task<bool> StudentCodeExistsAsync(Student student);
    Task<bool> StudentExistsAsync(Guid studentId);
    Task<Student?> FindStudentForParentLinkAsync(Guid studentId);
    Task<IdentityAccount?> FindParentByPhoneAsync(string? phone);
    Task<bool> ParentLinkExistsAsync(IdentityAccount parent, Guid studentId);
    Task<List<ParentChildSummary>> ListParentChildrenAsync(DateTimeOffset now, Guid userId);
    Task<bool> ParentCanAccessStudentAsync(Guid userId, ReportAbsence input);
    Task<bool> HasActiveEnrollmentAsync(DateOnly today, ReportAbsence input);
    Task<bool> HasOverlappingAbsenceAsync(ReportAbsence input);
    Task<MealAbsence?> FindReportedAbsenceAsync(Guid id, Guid userId);
    Task<MealAbsence> GetTrackedAbsenceAsync(Guid id);
    Task<bool> HasGuardianLinkAsync(Guid userId, MealAbsence absence);
    Task<bool> HasOtherOverlappingAbsenceAsync(Guid id, MealAbsence absence, ReportAbsence input);
    Task<List<AbsenceStudentOption>> ListAbsenceStudentOptionsAsync(Guid userId, CancellationToken ct);
    Task<int> CountReportedAbsencesAsync(AbsenceListFilter filter, CancellationToken ct);
    Task<List<AbsenceSummary>> SearchReportedAbsencesAsync(AbsenceListFilter filter, int page, int size, CancellationToken ct);
    Task<List<AbsenceSummary>> ListReportedAbsencesAsync(Guid reportedByUserId, Guid guardianUserId);
    Task<MealAbsence?> FindAbsenceForCancellationAsync(Guid id, Guid userId);
    Task<MealAbsence> GetAbsenceForCancellationAsync(Guid id);
    Task<bool> HasGuardianLinkForCancellationAsync(MealAbsence absence, Guid userId);
    Task<bool> SchoolYearHasClassesAsync(string? schoolYear);
    Task<bool> MealSessionExistsAsync(CreateMealDay input, string? mealType);
    Task<int> CountMealDaysAsync(DateOnly? date);
    Task<List<WorkflowMealDaySummary>> ListMealDaysAsync(DateOnly? date, int number);
    Task<MealDay?> FindMealDayAsync(Guid id, CancellationToken ct);
    Task<Guid[]> ListAssignedClassIdsAsync(CancellationToken ct, Guid userId);
    Task<bool> HasSettlementAsync(Guid id, CancellationToken ct);
    Task<string?> FindEnrollmentYearCodeAsync(DateOnly from, Guid studentId);
    Task<AcademicYear?> FindAcademicYearContainingPeriodAsync(string? code, DateOnly from, DateOnly to);
    Task<bool> HasTeacherAssignmentAsync(Guid classId, Guid userId);

    void AddAcademicYear(AcademicYear value);
    void AddMealAbsence(MealAbsence value);
    void AddSchoolClass(SchoolClass value);
    void AddParentStudent(ParentStudent value);
    void AddPortionSettlements(IEnumerable<PortionSettlement> value);
    void AddMealDay(MealDay value);
    void AddStudent(Student value);
    Task LockStudentAsync(Guid id, CancellationToken ct = default);
    Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct = default);
}

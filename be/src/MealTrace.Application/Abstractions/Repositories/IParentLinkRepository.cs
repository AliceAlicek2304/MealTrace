using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Students;
namespace MealTrace.Application.Abstractions.Repositories;

public interface IParentLinkRepository
{
    Task<Student?> FindStudentByCodeAsync(string code);
    Task<List<Student>> FindClassStudentsByNameAsync(Guid classId, DateOnly date, string name);
    Task<List<ParentLinkClass>> ClassesAsync(Guid? teacherId);
    Task<Guid?> CurrentClassAsync(Guid studentId, DateOnly date);
    Task UnlinkAsync(Guid parentId, Guid studentId);
    Task<Student?> FindStudentAsync(Guid id);
    Task<bool> TeacherCanManageAsync(Guid teacherId, Guid studentId, DateOnly date);
    Task<bool> LinkedAsync(Guid parentId, Guid studentId);
    Task<bool> PendingAsync(Guid parentId, Guid studentId);
    Task<int> PendingCountAsync(Guid parentId);
    Task<ParentLinkRequest?> FindAsync(Guid id);
    void Add(ParentLinkRequest request);
    void Link(Guid parentId, Guid studentId);
    Task<ParentLinkRequestPage> ListAsync(Guid? parentId, Guid? teacherId, DateOnly date, string? status, int page, int size, Guid? classId = null, string? schoolYear = null);
}

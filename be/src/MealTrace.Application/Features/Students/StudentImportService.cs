using System.Data;
using System.Text;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Students;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;

namespace MealTrace.Application.Features.Students;

public sealed class StudentImportService(IStudentSpreadsheetReader reader, IStudentImportRepository repository,
    ICurrentActor actor, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<StudentImportPreview>> PreviewAsync(Stream file, Guid classId, DateOnly startDate, CancellationToken ct)
    {
        if (!actor.IsInRole(RoleNames.Admin) || actor.UserId is null) return Result.Forbidden();
        if (startDate < SchoolTime.Today(clock.GetUtcNow()) || !await repository.ClassExistsAsync(classId, ct))
            return Result.Invalid("Chọn lớp có sẵn và ngày bắt đầu từ hôm nay.");
        StudentImportSheet sheet;
        try { sheet = reader.Read(file); }
        catch (InvalidStudentSpreadsheetException ex) { return Result.Invalid(ex.Message); }
        var existing = (await repository.ExistingNamesAsync(classId, ct)).Select(NameKey).ToHashSet();
        var duplicates = sheet.Rows.GroupBy(row => NameKey(row.FullName)).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var rows = sheet.Rows.Select(row => row with
        {
            Error = row.Error ?? (row.DateOfBirth > SchoolTime.Today(clock.GetUtcNow()) ? "Ngày sinh không được ở tương lai." : duplicates.Contains(NameKey(row.FullName)) ? "Trùng họ tên trong file; hãy kiểm tra hoặc thêm riêng từng trẻ." :
                existing.Contains(NameKey(row.FullName)) ? "Lớp đã có trẻ cùng họ tên; hãy kiểm tra để tránh nhập lại." : null)
        }).ToArray();
        return Result.Success(new StudentImportPreview(sheet.SheetName, sheet.IgnoredColumns, rows, rows.Length > 0 && rows.All(row => row.Error is null)));
    }

    public async Task<Result<StudentImportResult>> ImportAsync(Stream file, Guid classId, DateOnly startDate, CancellationToken ct)
    {
        if (!actor.IsInRole(RoleNames.Admin) || actor.UserId is null) return Result.Forbidden();
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var preview = await PreviewAsync(file, classId, startDate, ct);
        if (!preview.IsSuccess) return preview.Error!;
        // Re-read and validate inside the transaction. Preview never grants permission to bypass validation.
        if (!preview.Value!.CanImport) return Result.Invalid("File có dòng lỗi hoặc trùng. Sửa file và xem trước lại; chưa có trẻ nào được nhập.");
        foreach (var row in preview.Value.Rows)
        {
            var student = new Student { FullName = row.FullName, DateOfBirth = row.DateOfBirth, Gender = row.Gender, ClassId = classId };
            student.Enrollments.Add(new Enrollment { ClassId = classId, StartDate = startDate,
                RecordedByUserId = actor.UserId.Value, RecordedAt = clock.GetUtcNow() });
            repository.Add(student);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success(new StudentImportResult(preview.Value.Rows.Length));
    }

    private static string NameKey(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormKC).ToUpperInvariant();
}

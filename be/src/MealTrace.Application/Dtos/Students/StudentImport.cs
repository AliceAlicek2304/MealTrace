namespace MealTrace.Application.Dtos.Students;

public sealed record StudentImportRow(int Row, string FullName, string? Error, DateOnly? DateOfBirth = null, string? Gender = null, string? ParentPhoneNumber = null);
public sealed record StudentImportSheet(string SheetName, string[] IgnoredColumns, StudentImportRow[] Rows);
public sealed record StudentImportPreview(string SheetName, string[] IgnoredColumns, StudentImportRow[] Rows, bool CanImport);
public sealed record StudentImportResult(int Created, Guid BatchId);
public sealed record StudentImportBatchSummary(Guid Id, string FileName, string SheetName, Guid ClassId,
    string ClassName, string SchoolYear, DateOnly StartDate, int Created, DateTimeOffset ImportedAt, string ImportedByName);
public sealed record StudentImportHistory(StudentImportBatchSummary[] Items, int Total, int Page, int PageSize);
public sealed record StudentImportBatchDetail(StudentImportBatchSummary Batch, StudentImportRow[] Rows);

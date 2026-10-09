namespace MealTrace.Application.Dtos.Students;

public sealed record StudentImportRow(int Row, string FullName, string? Error, DateOnly? DateOfBirth = null, string? Gender = null, string? ParentPhoneNumber = null);
public sealed record StudentImportSheet(string SheetName, string[] IgnoredColumns, StudentImportRow[] Rows);
public sealed record StudentImportPreview(string SheetName, string[] IgnoredColumns, StudentImportRow[] Rows, bool CanImport);
public sealed record StudentImportResult(int Created);

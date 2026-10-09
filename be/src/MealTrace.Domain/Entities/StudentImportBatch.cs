namespace MealTrace.Domain.Entities;

public sealed class StudentImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public required string ClassName { get; set; }
    public required string SchoolYear { get; set; }
    public required string FileName { get; set; }
    public required string SheetName { get; set; }
    public Guid ImportedByUserId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public DateOnly StartDate { get; set; }
    public int Created { get; set; }
    public List<StudentImportBatchItem> Items { get; set; } = [];
}

public sealed class StudentImportBatchItem
{
    public Guid BatchId { get; set; }
    public Guid StudentId { get; set; }
    public int SourceRow { get; set; }
    public required string FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
}

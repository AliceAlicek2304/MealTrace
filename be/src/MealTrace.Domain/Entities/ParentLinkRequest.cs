namespace MealTrace.Domain.Entities;

public sealed class ParentLinkRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Relationship { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewReason { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public string? RevocationReason { get; set; }
    public int Revision { get; set; }
}

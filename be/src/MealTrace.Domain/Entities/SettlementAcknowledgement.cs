namespace MealTrace.Domain.Entities;

/// <summary>
/// Ghi nhận lịch sử Bếp tiếp nhận và xác nhận số suất ăn đã chốt (Append-only).
/// </summary>
public class SettlementAcknowledgement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public DateTimeOffset AcknowledgedAt { get; set; } = DateTimeOffset.UtcNow;
    public string AcknowledgedBy { get; set; } = string.Empty;
    public string? AcknowledgedByName { get; set; }
    public int PortionsCount { get; set; }
    public string? Note { get; set; }
}

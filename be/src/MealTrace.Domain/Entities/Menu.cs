namespace MealTrace.Domain.Entities;

/// <summary>The planned menu of one meal session (<see cref="MealDay"/>). Content lives in append-only versions.</summary>
public sealed class Menu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedByUserId { get; set; }
}

namespace MealTrace.Application.Dtos.Portions;

public sealed record RequestInput(Guid ClassId, Guid StudentId, Guid BaseSettlementId, bool WillEat, string Reason);

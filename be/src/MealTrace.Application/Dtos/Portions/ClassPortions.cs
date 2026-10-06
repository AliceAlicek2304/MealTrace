namespace MealTrace.Application.Dtos.Portions;

public sealed record ClassPortions(Guid ClassId, string ClassName, string SchoolYear,
List<Guid> StudentIds, List<string> StudentNames, List<Guid> AbsentStudentIds, bool IsSettled,
Guid? SettlementId = null, int Version = 0, int? OriginalCount = null, int? Count = null);

using MealTrace.Application.Dtos.Portions;

namespace MealTrace.Application.Dtos.Kitchen;

public record KitchenSettlementSummary(
    Guid MealDayId,
    DateOnly Date,
    string SessionType,
    DateTimeOffset CutoffAt,
    DateTimeOffset SettledAt,
    int OriginalPortions,
    int AppliedPortions,
    int Diff,
    bool IsAcknowledged,
    DateTimeOffset? LastAcknowledgedAt,
    string? LastAcknowledgedBy,
    string? LastAcknowledgedByName,
    List<KitchenClassSummary> Classes
);

public record KitchenClassSummary(
    Guid ClassId,
    string ClassName,
    int OriginalCount,
    int AppliedCount,
    int Diff,
    int KitchenAdjustment,
    int Version
);

public record KitchenSettlementDiffResponse(
    Guid MealDayId,
    DateOnly Date,
    string SessionType,
    DateTimeOffset CutoffAt,
    DateTimeOffset SettledAt,
    int OriginalTotal,
    int AppliedTotal,
    int TotalDiff,
    int TotalKitchenAdjustment,
    List<KitchenClassDiff> ClassDiffs
);

public record KitchenClassDiff(
    Guid ClassId,
    string ClassName,
    int OriginalCount,
    int AppliedCount,
    int Diff,
    int KitchenAdjustment,
    int CurrentVersion,
    List<SettlementStudentSummary> AddedStudents,
    List<SettlementStudentSummary> RemovedStudents
);

public record AcknowledgeSettlementRequest(string? Note);

public record SettlementAcknowledgementResponse(
    Guid Id,
    Guid MealDayId,
    DateTimeOffset AcknowledgedAt,
    string AcknowledgedBy,
    string? AcknowledgedByName,
    int PortionsCount,
    string? Note
);

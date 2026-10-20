using MealTrace.Application.Dtos.Portions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    List<KitchenClassSummary> Classes,
    string SnapshotToken
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
    List<KitchenClassDiff> ClassDiffs,
    string SnapshotToken,
    bool IsAcknowledged
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
    List<SettlementStudentSummary> RemovedStudents,
    bool HasCompleteRoster
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record AcknowledgeSettlementRequest(string? Note, [property: Required, StringLength(64, MinimumLength = 64)] string? SnapshotToken = null);

public record SettlementAcknowledgementResponse(
    Guid Id,
    Guid MealDayId,
    DateTimeOffset AcknowledgedAt,
    string AcknowledgedBy,
    string? AcknowledgedByName,
    int PortionsCount,
    string? Note,
    string? SnapshotToken
);

public record KitchenSettlementPage(List<KitchenSettlementSummary> Items, int Total, int Page, int PageSize);

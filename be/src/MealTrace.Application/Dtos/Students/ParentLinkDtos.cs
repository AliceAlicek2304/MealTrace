using System.Text.Json.Serialization;
namespace MealTrace.Application.Dtos.Students;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateParentLinkRequest(string? StudentCode, string? StudentName, string? Relationship, string? Note, Guid? ClassId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewParentLinkRequest(bool Approve, string? Reason, int Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelParentLinkRequest(int Revision);
public sealed record ParentLinkRequestView(Guid Id, string StudentCode, string StudentName, string Relationship, string Note,
    string Status, DateTimeOffset RequestedAt, DateTimeOffset? ReviewedAt, string? ReviewReason, int Revision,
    string ParentName, string? ParentPhone, string? ClassName, Guid? ClassId, DateTimeOffset? RevokedAt, string? RevocationReason);
public sealed record ParentLinkRequestPage(List<ParentLinkRequestView> Items, int Total, int Page, int PageSize);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParentLinkSelection(Guid Id, int Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BulkReviewParentLinks(Guid? ClassId, List<ParentLinkSelection>? Items, bool Approve, string? Reason, string? SchoolYear = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeParentLink(int Revision, string? Reason);
public sealed record ParentLinkClass(Guid Id, string Name, string SchoolYear);

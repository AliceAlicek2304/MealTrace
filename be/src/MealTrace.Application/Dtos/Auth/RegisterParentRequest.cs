using System.Text.Json.Serialization;

namespace MealTrace.Application.Dtos.Auth;

// Public registration accepts no roles, scopes, status or verification flags.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterParentRequest(string? FullName, string? PhoneNumber, string? Password, Guid? ChallengeId, string? OtpCode);

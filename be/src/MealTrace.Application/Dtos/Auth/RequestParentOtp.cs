using System.Text.Json.Serialization;
namespace MealTrace.Application.Dtos.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestParentOtp(string? PhoneNumber);
public sealed record ParentOtpResponse(Guid ChallengeId, DateTimeOffset ExpiresAt, DateTimeOffset ResendAt, string Message);

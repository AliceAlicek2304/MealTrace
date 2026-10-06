using MealTrace.Application.Dtos.Notifications;
using MealTrace.Application.Features.Notifications;
using MealTrace.Domain.Security;

namespace MealTrace.Api.Features;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/settings", (NotificationPolicy policy) =>
            Results.Ok(new NotificationSettingsResponse(policy.Enabled, policy.Channel, policy.TemplateOnly)))
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher)).WithTags("Notifications")
            .Produces<NotificationSettingsResponse>();
        app.MapPost("/api/admin/notifications/test", async (NotificationService service, CancellationToken ct) =>
        {
            var result = await service.SendTestAsync(ct);
            if (!result.IsSuccess) return result.ToHttpResult();
            var body = result.Value!;
            var status = body.Status switch { "ACCEPTED" => 202, "DISABLED" => 503, "UNKNOWN" => 504, _ => 502 };
            return Results.Json(body, statusCode: status);
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin)).WithTags("Notifications")
            .WithName("SendTestNotification").WithSummary("Gửi một tin mẫu qua kênh cấu hình WhatsApp tới tester; dùng hạn mức/tín dụng nhà cung cấp.")
            .Produces<NotificationResponse>(202).Produces<NotificationResponse>(502).Produces<NotificationResponse>(503).Produces<NotificationResponse>(504)
            .AddEndpointFilter(async (context, next) => { context.HttpContext.Response.Headers.CacheControl = "no-store"; return await next(context); });
        return app;
    }
}

using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Domain.Security;

namespace MealTrace.Api.Features;

internal static class KitchenSettlementEndpoints
{
    public static void MapKitchenSettlementEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/settlements").WithTags("Kitchen - Settlements");
        Describe(group.MapGet("", async (DateOnly? date, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.ListSettlementsAsync(date, ct)))
            .Produces<List<KitchenSettlementSummary>>().WithName("KitchenSettlements")
            .WithSummary("Danh sách bản chốt (tối đa 100 phiên)")
            .WithDescription("Admin/Bếp. API tương thích; dùng /search để xem đầy đủ danh sách có phân trang."));
        Describe(group.MapGet("/search", async (DateOnly? date, int? page, int? pageSize, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.SearchSettlementsAsync(date, page, pageSize, ct)))
            .Produces<KitchenSettlementPage>().WithName("SearchKitchenSettlements")
            .WithSummary("Tìm bản chốt có phân trang")
            .WithDescription("Lọc ngày tùy chọn; page mặc định 1, pageSize mặc định 20, tối đa 100. Chỉ phiên đã chốt và chưa hủy. IsAcknowledged áp dụng cho bản hiện hành."));
        Describe(group.MapGet("/{id:guid}/diff", async (Guid id, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.GetSettlementDiffAsync(id, ct)))
            .Produces<KitchenSettlementDiffResponse>().ProducesProblem(404).ProducesProblem(409)
            .WithName("KitchenSettlementDiff").WithSummary("Đối chiếu bản gốc với bản hiện hành")
            .WithDescription("Giữ SnapshotToken từ kết quả này để xác nhận. HasCompleteRoster=false: không đủ dữ liệu so sánh trẻ, không suy diễn từ danh sách thiếu."));
        Describe(group.MapPost("/{id:guid}/acknowledge", async (Guid id, AcknowledgeSettlementRequest request, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Json(await service.AcknowledgeSettlementAsync(id, request, ct), statusCode: StatusCodes.Status201Created))
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff)
                .RequireAssertion(context => !context.User.IsInRole(RoleNames.Admin)))
            .Produces<SettlementAcknowledgementResponse>(201).ProducesProblem(404).ProducesProblem(409)
            .WithName("AcknowledgeKitchenSettlement").WithSummary("Bếp xác nhận bản suất đã đối chiếu")
            .WithDescription("Chỉ KITCHEN_STAFF được xác nhận; ADMIN chỉ xem đối chiếu và lịch sử. Bắt buộc SnapshotToken lấy từ /diff; Note tùy chọn, tối đa 500 ký tự. Token cũ trả 409, cần tải lại đối chiếu. Gửi lại cùng người/bản suất trả bản ghi đã có. Bản mới cần xác nhận mới; lịch sử cũ được giữ."));
        Describe(group.MapGet("/{id:guid}/acknowledgements", async (Guid id, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.ListAcknowledgementsAsync(id, ct)))
            .Produces<List<SettlementAcknowledgementResponse>>().ProducesProblem(404)
            .WithName("KitchenSettlementAcknowledgements").WithSummary("Lịch sử tiếp nhận của bếp")
            .WithDescription("Bản ghi cũ không có SnapshotToken chỉ là lịch sử, không xác nhận bản hiện hành."));
    }

    private static void Describe(RouteHandlerBuilder route) => route.ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
}

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MealTrace.Api.Features;

// Manual parsing is intentional: invalid/duplicate queries use the audit problem
// contract. Describe them explicitly because HttpRequest is invisible to Swagger.
internal sealed class AuditOpenApiFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.Split('?')[0].TrimEnd('/');
        const string root = "api/admin/audit-logs";
        if (path is null || !(path == root || path.StartsWith(root + "/", StringComparison.Ordinal))) return;
        operation.Description = "Chỉ ADMIN. Lịch sử chỉ đọc; metadata nguồn thiếu trả null. Cache-Control: no-store.";
        var export = path == root + "/export";
        var timeline = path.StartsWith(root + "/entities/", StringComparison.Ordinal);
        if (path == root || export || timeline)
        {
            Add("from", new() { Type = "string", Format = "date-time" }, "Instant gồm cả biên; cần Z hoặc UTC offset. Encode + thành %2B.", export);
            Add("to", new() { Type = "string", Format = "date-time" }, export ? "Instant gồm cả biên; khoảng tối đa 31 ngày, tối đa 10.000 event." : "Instant gồm cả biên; from <= to.", export);
            Add("actorId", new() { Type = "string", Format = "uuid" }, "Actor ID đã được nguồn ghi nhận; không dùng tên account hiện tại.");
            Add("action", new() { Type = "string", MaxLength = 100 }, "Ví dụ calendar.generate, account.password_reset, portion.settle.");
            if (!timeline)
            {
                Add("entityType", new() { Type = "string", MaxLength = 100 }, "Student, MealCalendar, Account, PortionAmendment, PortionSettlement, ParentLinkRequest hoặc StudentImportBatch.");
                Add("entityId", new() { Type = "string", MaxLength = 200 }, "GUID entity; MealCalendar dùng mã năm học. Student + ID mở rộng qua FK/membership thực.");
            }
            if (export) Add("format", new() { Type = "string", Default = new OpenApiString("csv"), Enum = [new OpenApiString("csv")] }, "CSV UTF-8 BOM, escape và masking điện thoại.");
            else
            {
                Add("page", new() { Type = "integer", Format = "int32", Minimum = 1, Default = new OpenApiInteger(1) }, "Trang bắt đầu từ 1. Dataset thay đổi có thể dịch offset.");
                Add("pageSize", new() { Type = "integer", Format = "int32", Minimum = 1, Maximum = 100, Default = new OpenApiInteger(25) }, "Mặc định 25, tối đa 100. Sort occurredAt DESC, source ASC, id ASC.");
            }
        }
        foreach (var parameter in operation.Parameters.Where(p => p.In == ParameterLocation.Path))
            parameter.Description = parameter.Name switch
            {
                "auditId" => "source:guid dạng D. Lấy id từ API list; unknown source 400, missing event 404.",
                "entityType" => "Student mở rộng qua FK/membership; entity khác chỉ match primary identity.",
                _ => "ID entity; không suy diễn quan hệ qua timestamp."
            };
        foreach (var response in operation.Responses)
        {
            response.Value.Headers["Cache-Control"] = new OpenApiHeader
                { Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString("no-store") } };
            if (!response.Key.StartsWith('2') && response.Value.Content.TryGetValue("application/problem+json", out var content))
                content.Schema = new OpenApiSchema
                {
                    AllOf = [content.Schema], Properties = new Dictionary<string, OpenApiSchema> { ["code"] = new() { Type = "string" } },
                    Required = new HashSet<string> { "code" }
                };
        }
        void Add(string name, OpenApiSchema schema, string description, bool required = false)
            => operation.Parameters.Add(new OpenApiParameter { Name = name, In = ParameterLocation.Query,
                Schema = schema, Description = description, Required = required });
    }
}

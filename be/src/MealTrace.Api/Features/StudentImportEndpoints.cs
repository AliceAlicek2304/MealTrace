using System.Globalization;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Students;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Features.Students;
using MealTrace.Domain.Security;
using Microsoft.AspNetCore.Http.Features;

namespace MealTrace.Api.Features;

public static class StudentImportEndpoints
{
    public static IEndpointRouteBuilder MapStudentImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/students/import").WithTags("Student import")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (PersistenceConflictException) { return Results.Conflict(new MessageResponse("Danh sách lớp vừa thay đổi. Hãy xem trước lại file; chưa nhập danh sách này.")); }
            catch (InvalidDataException) { return Results.BadRequest(new MessageResponse("File hoặc biểu mẫu không hợp lệ, tối đa 5 MB.")); }
            catch (BadHttpRequestException) { return Results.BadRequest(new MessageResponse("Yêu cầu tải file không hợp lệ, tối đa 5 MB.")); }
        });
        group.MapPost("/preview", (HttpContext context, StudentImportService service, CancellationToken ct) => Handle(context, service, false, ct))
            .Accepts<StudentImportUpload>("multipart/form-data").Produces<StudentImportPreview>().WithName("PreviewStudentImport");
        group.MapPost("/confirm", (HttpContext context, StudentImportService service, CancellationToken ct) => Handle(context, service, true, ct))
            .Accepts<StudentImportUpload>("multipart/form-data").Produces<StudentImportResult>().WithName("ConfirmStudentImport");
        return app;
    }

    private static async Task<IResult> Handle(HttpContext context, StudentImportService service, bool confirm, CancellationToken ct)
    {
        if (!context.Request.HasFormContentType || context.Request.ContentLength > 6 * 1024 * 1024)
            return Results.BadRequest(new MessageResponse("Tải file XLSX tối đa 5 MB."));
        context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions
        { MultipartBodyLengthLimit = 5 * 1024 * 1024, ValueLengthLimit = 256, ValueCountLimit = 10, MultipartHeadersLengthLimit = 4096 }));
        var form = await context.Request.ReadFormAsync(ct);
        if (form.Files.Count != 1 || form.Files[0].Name != "file" || form.Files[0].Length is 0 or > 5 * 1024 * 1024 ||
            !Path.GetExtension(form.Files[0].FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(form["classId"], out var classId) ||
            !DateOnly.TryParseExact(form["startDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
            return Results.BadRequest(new MessageResponse("Cần file XLSX, lớp và ngày bắt đầu hợp lệ."));
        await using var file = form.Files[0].OpenReadStream();
        return confirm ? (await service.ImportAsync(file, classId, startDate, ct)).ToHttpResult() :
            (await service.PreviewAsync(file, classId, startDate, ct)).ToHttpResult();
    }

    public sealed record StudentImportUpload(IFormFile File, Guid ClassId, DateOnly StartDate);
}

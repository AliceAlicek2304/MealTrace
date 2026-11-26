using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public sealed record ServingWriteResult(ServingResponse Response, bool IsReplay);

public interface IServingService
{
    Task<ServingResponse> RecordServingAsync(CreateServingRequest request, CancellationToken ct = default);
    Task<ServingWriteResult> RecordServingWithStatusAsync(CreateServingRequest request, CancellationToken ct = default);
    Task<ServingDetailResponse?> GetServingAsync(Guid id, CancellationToken ct = default);
    Task<ServingResponse> AddServingVersionAsync(Guid id, CreateServingVersionRequest request, CancellationToken ct = default);
    Task<List<ServingHistoryItem>> GetServingHistoryAsync(Guid id, CancellationToken ct = default);
}

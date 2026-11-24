using MealTrace.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MealTrace.Application.Abstractions.Repositories
{
    public interface IServingRepository
    {

        Task<ServingVersion?> FindVersionByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct = default);

        Task<Serving?> FindServingWithVersionAsync(Guid servingId, CancellationToken ct = default);

        Task<Serving?> FindServingByClassAndMealDayAsync(Guid mealDayId, Guid classId, CancellationToken ct = default);

        Task<bool> MealDayExistsAsync(Guid mealDayId, CancellationToken ct = default);
        Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct = default);
        Task<bool> SettlementSnapshotMatchesAsync(Guid settlementId, string snapshotToken, CancellationToken ct = default);

        void AddServing(Serving serving);
        void AddServingVersion(ServingVersion version);
    }
}

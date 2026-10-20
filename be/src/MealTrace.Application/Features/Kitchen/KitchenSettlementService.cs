using System.Data;
using System.Security.Cryptography;
using System.Text;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Dtos.Portions;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

public sealed class KitchenSettlementService(IKitchenSettlementRepository repository, ICurrentActor actor, TimeProvider clock, IUnitOfWork unitOfWork) : IKitchenSettlementService
{
    private static IEnumerable<IGrouping<Guid, PortionSettlement>> Groups(MealDay day) => day.Settlements
        .Where(s => s.ClassId.HasValue).GroupBy(s => s.ClassId!.Value);

    private static string Token(MealDay day)
    {
        var versions = Groups(day).OrderBy(g => g.Key).Select(g => g.OrderByDescending(s => s.Version).First().Id.ToString("N"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{day.Id:N}:{string.Join(":", versions)}")));
    }

    private static bool Acknowledged(MealDay day, string token) => day.Acknowledgements.Any(a => a.SnapshotToken == token);
    private static bool Complete(PortionSettlement snapshot) => snapshot.Count - snapshot.KitchenAdjustment == snapshot.Students.Count;
    private static SettlementStudentSummary Student(SettlementStudent child) => new() { StudentId = child.StudentId, StudentName = child.StudentName };

    private static KitchenSettlementSummary Summary(MealDay day)
    {
        var classes = Groups(day).Select(g => {
            var original = g.OrderBy(s => s.Version).First();
            var latest = g.OrderByDescending(s => s.Version).First();
            return new KitchenClassSummary(g.Key, latest.ClassName ?? original.ClassName ?? "Lớp học", original.Count,
                latest.Count, latest.Count - original.Count, latest.KitchenAdjustment, latest.Version);
        }).OrderBy(c => c.ClassName).ToList();
        var ack = day.Acknowledgements.OrderByDescending(a => a.AcknowledgedAt).ThenBy(a => a.Id).FirstOrDefault();
        var token = Token(day);
        return new(day.Id, day.Date, day.MealType, day.CutoffAt, day.SettledAt!.Value,
            classes.Sum(c => c.OriginalCount), classes.Sum(c => c.AppliedCount), classes.Sum(c => c.Diff),
            Acknowledged(day, token), ack?.AcknowledgedAt, ack?.AcknowledgedBy, ack?.AcknowledgedByName, classes, token);
    }

    // Bounded compatibility endpoint. Interactive clients use the paginated search endpoint.
    public async Task<List<KitchenSettlementSummary>> ListSettlementsAsync(DateOnly? date, CancellationToken ct) =>
        (await repository.ListSettledMealDaysAsync(date, 0, 100, ct)).Select(Summary).ToList();

    public async Task<KitchenSettlementPage> SearchSettlementsAsync(DateOnly? date, int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var total = await repository.CountSettledMealDaysAsync(date, ct);
        var days = await repository.ListSettledMealDaysAsync(date, (number - 1) * size, size, ct);
        return new(days.Select(Summary).ToList(), total, number, size);
    }

    private async Task<MealDay> ReadSettled(Guid id, CancellationToken ct)
    {
        var day = await repository.FindSettledMealDayWithDetailsAsync(id, ct)
            ?? throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");
        if (day.IsCancelled) throw new KitchenException(409, "CANCELLED", "Phiên ăn đã bị hủy.");
        if (day.SettledAt is null || !Groups(day).Any())
            throw new KitchenException(409, "NOT_SETTLED", "Phiên ăn chưa có bản chốt theo lớp để đối chiếu.");
        return day;
    }

    private static KitchenClassDiff ClassDiff(IGrouping<Guid, PortionSettlement> group)
    {
        var original = group.OrderBy(s => s.Version).First();
        var latest = group.OrderByDescending(s => s.Version).First();
        var complete = Complete(original) && Complete(latest);
        var originalIds = original.Students.Select(s => s.StudentId).ToHashSet();
        var latestIds = latest.Students.Select(s => s.StudentId).ToHashSet();
        return new(group.Key, latest.ClassName ?? original.ClassName ?? "Lớp học", original.Count, latest.Count,
            latest.Count - original.Count, latest.KitchenAdjustment, latest.Version,
            complete ? latest.Students.Where(s => !originalIds.Contains(s.StudentId)).Select(Student).ToList() : [],
            complete ? original.Students.Where(s => !latestIds.Contains(s.StudentId)).Select(Student).ToList() : [], complete);
    }

    public async Task<KitchenSettlementDiffResponse> GetSettlementDiffAsync(Guid mealDayId, CancellationToken ct)
    {
        var day = await ReadSettled(mealDayId, ct);
        var classes = Groups(day).Select(ClassDiff).OrderBy(c => c.ClassName).ToList();
        var token = Token(day);
        return new(day.Id, day.Date, day.MealType, day.CutoffAt, day.SettledAt!.Value,
            classes.Sum(c => c.OriginalCount), classes.Sum(c => c.AppliedCount), classes.Sum(c => c.Diff),
            classes.Sum(c => c.KitchenAdjustment), classes, token, Acknowledged(day, token));
    }

    public async Task<SettlementAcknowledgementResponse> AcknowledgeSettlementAsync(Guid mealDayId, AcknowledgeSettlementRequest request, CancellationToken ct)
    {
        if (request.Note?.Trim().Length > 500 || request.SnapshotToken is null || request.SnapshotToken.Length != 64 || !request.SnapshotToken.All(Uri.IsHexDigit))
            throw new KitchenException(400, "INVALID_INPUT", "Cần đối chiếu bản suất hiện tại; ghi chú tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await repository.LockMealDayAsync(mealDayId, ct) is null)
            throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");
        var day = await ReadSettled(mealDayId, ct);
        var token = Token(day);
        if (request.SnapshotToken != token)
            throw new KitchenException(409, "STALE_SETTLEMENT", "Bản suất đã thay đổi. Vui lòng tải lại và đối chiếu trước khi xác nhận.");
        // Repeat taps are idempotent for the same actor and immutable snapshot.
        var actorId = actor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
        var ack = day.Acknowledgements.FirstOrDefault(a => a.SnapshotToken == token && a.AcknowledgedBy == actorId.ToString());
        if (ack is null)
        {
            ack = new SettlementAcknowledgement {
                MealDayId = mealDayId, AcknowledgedAt = clock.GetUtcNow(), AcknowledgedBy = actorId.ToString(),
                AcknowledgedByName = await repository.GetActorNameAsync(actorId, ct), SnapshotToken = token,
                PortionsCount = Groups(day).Sum(g => g.OrderByDescending(s => s.Version).First().Count),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
            };
            repository.AddAcknowledgement(ack);
            await unitOfWork.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync();
        return Response(ack);
    }

    private static SettlementAcknowledgementResponse Response(SettlementAcknowledgement ack) => new(ack.Id, ack.MealDayId,
        ack.AcknowledgedAt, ack.AcknowledgedBy, ack.AcknowledgedByName, ack.PortionsCount, ack.Note, ack.SnapshotToken);

    public async Task<List<SettlementAcknowledgementResponse>> ListAcknowledgementsAsync(Guid mealDayId, CancellationToken ct)
    {
        if (await repository.FindSettledMealDayWithDetailsAsync(mealDayId, ct) is null)
            throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");
        return (await repository.ListAcknowledgementsAsync(mealDayId, ct)).Select(Response).ToList();
    }
}

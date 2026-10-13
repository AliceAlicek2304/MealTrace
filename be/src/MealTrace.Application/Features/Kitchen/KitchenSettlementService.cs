using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Dtos.Portions;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

public class KitchenSettlementService : IKitchenSettlementService
{
    private readonly IKitchenSettlementRepository _repository;
    private readonly ICurrentActor _currentActor;
    private readonly TimeProvider _clock;
    private readonly IUnitOfWork _unitOfWork;

    public KitchenSettlementService(
        IKitchenSettlementRepository repository,
        ICurrentActor currentActor,
        TimeProvider clock,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentActor = currentActor;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<KitchenSettlementSummary>> ListSettlementsAsync(DateOnly? date, CancellationToken ct)
    {
        var days = await _repository.ListSettledMealDaysAsync(date, ct);
        var summaries = new List<KitchenSettlementSummary>();

        foreach (var day in days)
        {
            var classGroups = day.Settlements
                .Where(s => s.ClassId != null)
                .GroupBy(s => s.ClassId!.Value)
                .ToList();

            var classSummaries = new List<KitchenClassSummary>();
            var originalTotal = 0;
            var appliedTotal = 0;

            foreach (var group in classGroups)
            {
                var orig = group.OrderBy(s => s.Version).First();
                var latest = group.OrderByDescending(s => s.Version).First();

                originalTotal += orig.Count;
                appliedTotal += latest.Count;

                classSummaries.Add(new KitchenClassSummary(
                    ClassId: group.Key,
                    ClassName: latest.ClassName ?? orig.ClassName ?? "Lớp học",
                    OriginalCount: orig.Count,
                    AppliedCount: latest.Count,
                    Diff: latest.Count - orig.Count,
                    KitchenAdjustment: latest.KitchenAdjustment,
                    Version: latest.Version
                ));
            }

            var latestAck = day.Acknowledgements.OrderByDescending(a => a.AcknowledgedAt).FirstOrDefault();

            summaries.Add(new KitchenSettlementSummary(
                MealDayId: day.Id,
                Date: day.Date,
                SessionType: day.MealType,
                CutoffAt: day.CutoffAt,
                SettledAt: day.SettledAt ?? DateTimeOffset.MinValue,
                OriginalPortions: originalTotal,
                AppliedPortions: appliedTotal,
                Diff: appliedTotal - originalTotal,
                IsAcknowledged: day.Acknowledgements.Count > 0,
                LastAcknowledgedAt: latestAck?.AcknowledgedAt,
                LastAcknowledgedBy: latestAck?.AcknowledgedBy,
                LastAcknowledgedByName: latestAck?.AcknowledgedByName,
                Classes: classSummaries.OrderBy(c => c.ClassName).ToList()
            ));
        }

        return summaries;
    }

    public async Task<KitchenSettlementDiffResponse> GetSettlementDiffAsync(Guid mealDayId, CancellationToken ct)
    {
        var day = await _repository.FindSettledMealDayWithDetailsAsync(mealDayId, ct);
        if (day is null)
            throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");

        if (day.SettledAt is null)
            throw new KitchenException(409, "NOT_SETTLED", "Phiên ăn chưa có bản chốt suất.");

        var classGroups = day.Settlements
            .Where(s => s.ClassId != null)
            .GroupBy(s => s.ClassId!.Value)
            .ToList();

        var classDiffs = new List<KitchenClassDiff>();
        var originalTotal = 0;
        var appliedTotal = 0;
        var totalKitchenAdj = 0;

        foreach (var group in classGroups)
        {
            var orig = group.OrderBy(s => s.Version).First();
            var latest = group.OrderByDescending(s => s.Version).First();

            originalTotal += orig.Count;
            appliedTotal += latest.Count;
            totalKitchenAdj += latest.KitchenAdjustment;

            // 1. Tìm học sinh được bổ sung sau 07:30 (có trong bản mới nhất nhưng chưa có trong bản chốt gốc)
            var added = new List<SettlementStudentSummary>();
            foreach (var student in latest.Students)
            {
                bool daCoTuSang = false;
                foreach (var origStudent in orig.Students)
                {
                    if (origStudent.StudentId == student.StudentId)
                    {
                        daCoTuSang = true;
                        break;
                    }
                }

                if (!daCoTuSang)
                {
                    added.Add(new SettlementStudentSummary
                    {
                        StudentId = student.StudentId,
                        StudentName = student.StudentName
                    });
                }
            }

            // 2. Tìm học sinh bị rút bớt sau 07:30 (có trong bản chốt gốc nhưng vắng trong bản mới nhất)
            var removed = new List<SettlementStudentSummary>();
            foreach (var origStudent in orig.Students)
            {
                bool conAnKhong = false;
                foreach (var student in latest.Students)
                {
                    if (student.StudentId == origStudent.StudentId)
                    {
                        conAnKhong = true;
                        break;
                    }
                }

                if (!conAnKhong)
                {
                    removed.Add(new SettlementStudentSummary
                    {
                        StudentId = origStudent.StudentId,
                        StudentName = origStudent.StudentName
                    });
                }
            }

            classDiffs.Add(new KitchenClassDiff(
                ClassId: group.Key,
                ClassName: latest.ClassName ?? orig.ClassName ?? "Lớp học",
                OriginalCount: orig.Count,
                AppliedCount: latest.Count,
                Diff: latest.Count - orig.Count,
                KitchenAdjustment: latest.KitchenAdjustment,
                CurrentVersion: latest.Version,
                AddedStudents: added,
                RemovedStudents: removed
            ));
        }

        return new KitchenSettlementDiffResponse(
            MealDayId: day.Id,
            Date: day.Date,
            SessionType: day.MealType,
            CutoffAt: day.CutoffAt,
            SettledAt: day.SettledAt.Value,
            OriginalTotal: originalTotal,
            AppliedTotal: appliedTotal,
            TotalDiff: appliedTotal - originalTotal,
            TotalKitchenAdjustment: totalKitchenAdj,
            ClassDiffs: classDiffs.OrderBy(c => c.ClassName).ToList()
        );
    }

    public async Task<SettlementAcknowledgementResponse> AcknowledgeSettlementAsync(
        Guid mealDayId, AcknowledgeSettlementRequest request, CancellationToken ct)
    {
        if (request.Note != null && request.Note.Trim().Length > 500)
            throw new KitchenException(400, "INVALID_NOTE", "Ghi chú không được vượt quá 500 ký tự.");

        var day = await _repository.FindSettledMealDayWithDetailsAsync(mealDayId, ct);
        if (day is null)
            throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");

        if (day.SettledAt is null)
            throw new KitchenException(409, "NOT_SETTLED", "Phiên ăn chưa có bản chốt suất; Bếp chưa thể xác nhận.");

        var appliedTotal = day.Settlements
            .Where(s => s.ClassId != null)
            .GroupBy(s => s.ClassId!.Value)
            .Sum(g => g.OrderByDescending(s => s.Version).First().Count);

        var actorId = _currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
        var actorName = await _repository.GetActorNameAsync(actorId, ct);

        var ack = new SettlementAcknowledgement
        {
            MealDayId = mealDayId,
            AcknowledgedAt = _clock.GetUtcNow(),
            AcknowledgedBy = actorId.ToString(),
            AcknowledgedByName = actorName,
            PortionsCount = appliedTotal,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        _repository.AddAcknowledgement(ack);
        await _unitOfWork.SaveChangesAsync(ct);

        return new SettlementAcknowledgementResponse(
            Id: ack.Id,
            MealDayId: ack.MealDayId,
            AcknowledgedAt: ack.AcknowledgedAt,
            AcknowledgedBy: ack.AcknowledgedBy,
            AcknowledgedByName: ack.AcknowledgedByName,
            PortionsCount: ack.PortionsCount,
            Note: ack.Note
        );
    }

    public async Task<List<SettlementAcknowledgementResponse>> ListAcknowledgementsAsync(Guid mealDayId, CancellationToken ct)
    {
        var day = await _repository.FindSettledMealDayWithDetailsAsync(mealDayId, ct);
        if (day is null)
            throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");

        var acks = await _repository.ListAcknowledgementsAsync(mealDayId, ct);
        return acks.Select(a => new SettlementAcknowledgementResponse(
            Id: a.Id,
            MealDayId: a.MealDayId,
            AcknowledgedAt: a.AcknowledgedAt,
            AcknowledgedBy: a.AcknowledgedBy,
            AcknowledgedByName: a.AcknowledgedByName,
            PortionsCount: a.PortionsCount,
            Note: a.Note
        )).ToList();
    }
}

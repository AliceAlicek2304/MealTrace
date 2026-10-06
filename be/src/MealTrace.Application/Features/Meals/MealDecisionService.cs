using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Models.Persistence;
using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Meals;

public sealed class MealDecisionService(IMealDecisionRepository repository)
{

    public static string Action(bool? willEat) => willEat switch { true => "EAT", false => "ABSENT", null => "DEFAULT" };

    public async Task<List<MealDecision>> ReadAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses = null, CancellationToken ct = default)
    {
        if (day.IsCancelled || allowedClasses is { Length: 0 }) return [];
        var members = await repository.ListEligibleMembersAsync(day, now, allowedClasses, ct);
        return await ResolveAsync(day, now, members, ct);
    }

    // Page enrollment rows before loading evidence. Dropdown classes stay independent of filters/page.
    public async Task<DecisionPage> ReadPageAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, Guid? classId, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (day.IsCancelled || allowedClasses is { Length: 0 }) return new([], 0, []);
        var classes = await repository.ListEligibleClassesAsync(day, now, allowedClasses, ct);
        var total = await repository.CountEligibleMembersAsync(day, now, allowedClasses, classId, search, ct);
        var members = await repository.SearchEligibleMembersAsync(day, now, allowedClasses, classId, search, page, pageSize, ct);
        return new(await ResolveAsync(day, now, members, ct), total, classes);
    }

    private async Task<List<MealDecision>> ResolveAsync(MealDay day, DateTimeOffset now, List<MealEnrollmentMember> members, CancellationToken ct)
    {
        if (members.Count == 0) return [];
        var asOf = now < day.CutoffAt ? now : day.CutoffAt;
        var ids = members.Select(x => x.StudentId).ToArray();
        var evidence = await repository.ListDecisionEvidenceAsync(day, asOf, members, ids, ct);
        var absenceByStudent = new Dictionary<Guid, Guid>();
        foreach (var absence in evidence.Where(x => x.Kind == 0)) absenceByStudent.TryAdd(absence.StudentId, absence.Id);
        var latest = evidence.Where(x => x.Kind == 1).ToDictionary(x => x.StudentId);
        return members.Select(x =>
        {
            Guid? absenceId = absenceByStudent.TryGetValue(x.StudentId, out var absence) ? absence : null;
            latest.TryGetValue(x.StudentId, out var exception);
            var willEat = exception?.WillEat ?? (absenceId is null);
            var source = exception?.WillEat switch { true => "STAFF_EAT", false => "STAFF_ABSENT", null => absenceId is null ? "DEFAULT" : "PARENT_ABSENCE" };
            return new MealDecision(x.StudentId, x.StudentCode, x.FullName, x.EnrollmentId, x.ClassId, x.ClassName, x.SchoolYear,
                willEat, source, absenceId is not null, absenceId, exception?.Id,
                exception is null ? null : Action(exception.WillEat), exception?.Reason);
        }).ToList();
    }
}

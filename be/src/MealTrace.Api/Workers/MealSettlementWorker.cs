using MealTrace.Application.Features.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MealTrace.Api.Workers;

public class MealSettlementWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MealSettlementWorker> logger,
    TimeProvider timeProvider,
    TimeSpan? checkPeriod = null) : BackgroundService
{
    private readonly TimeSpan _period = checkPeriod ?? TimeSpan.FromSeconds(30);

    public async Task<int> ProcessPendingSettlementsAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<WorkflowService>();

        List<Guid> pendingIds;
        try
        {
            pendingIds = await workflowService.GetPendingSettlementMealDayIdsAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query pending settlement meal days.");
            return 0;
        }

        if (pendingIds.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("Found {Count} pending meal day(s) for automatic settlement.", pendingIds.Count);

        var settledCount = 0;
        foreach (var mealDayId in pendingIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var result = await workflowService.SettlePortionsAsync(mealDayId, actor: "system", ct: ct);
                if (result.IsSuccess)
                {
                    settledCount++;
                    logger.LogInformation(
                        "Automatic settlement succeeded for meal day {MealDayId}. Classes: {Classes}, Portions: {Total}.",
                        mealDayId,
                        result.Value?.Classes,
                        result.Value?.Total);
                }
                else
                {
                    logger.LogWarning(
                        "Auto-settlement for meal session {Id} resulted in {Kind}: {Message}",
                        mealDayId,
                        result.Error?.Kind,
                        result.Error?.Message);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during automatic settlement for meal day {MealDayId}.", mealDayId);
            }
        }

        return settledCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MealSettlementWorker started with interval {Interval}.", _period);

        using var timer = new PeriodicTimer(_period, timeProvider);

        try
        {
            // Initial run upon worker startup to recover any delayed jobs immediately
            await ProcessPendingSettlementsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingSettlementsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("MealSettlementWorker is stopping as cancellation was requested.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in MealSettlementWorker execution loop.");
        }
    }
}

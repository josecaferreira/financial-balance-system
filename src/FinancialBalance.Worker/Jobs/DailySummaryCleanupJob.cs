using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinancialBalance.Worker.Infrastructure.Persistence;

namespace FinancialBalance.Worker.Jobs;

/// <summary>
/// Runs weekly to purge daily summaries older than the configured retention period (default: 5 years).
/// Monthly summaries are kept indefinitely (small table).
/// </summary>
public class DailySummaryCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailySummaryCleanupJob> _logger;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(365 * 5);

    public DailySummaryCleanupJob(IServiceScopeFactory scopeFactory, ILogger<DailySummaryCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailySummaryCleanupJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Run weekly on Sunday at 02:00 UTC
            var nextRun = ComputeNextRunUtc();
            var delay = nextRun - DateTime.UtcNow;

            _logger.LogInformation("Next cleanup scheduled at {NextRun} UTC", nextRun);

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await RunAsync(stoppingToken);
        }
    }

    private static DateTime ComputeNextRunUtc()
    {
        var now = DateTime.UtcNow;
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
        var nextSunday = now.Date.AddDays(daysUntilSunday == 0 ? 7 : daysUntilSunday).AddHours(2);
        return nextSunday > now ? nextSunday : nextSunday.AddDays(7);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily summary cleanup (retention: {Retention} days)", Retention.TotalDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow - Retention);

        try
        {
            var deleted = await db.DailySummaries
                .Where(d => d.Date < cutoff)
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation("Deleted {Count} daily summaries older than {Cutoff}", deleted, cutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily summary cleanup failed");
        }
    }
}

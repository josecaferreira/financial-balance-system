using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialBalance.Worker.Jobs;

/// <summary>
/// Runs daily at 00:05 UTC to compute MonthlySummaries from DailySummaries.
/// In production this service is replaced by a dedicated Kubernetes CronJob;
/// here it handles dev and staging environments automatically.
/// </summary>
public class MonthlyRollupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonthlyRollupJob> _logger;

    public MonthlyRollupJob(IServiceScopeFactory scopeFactory, ILogger<MonthlyRollupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonthlyRollupJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = ComputeNextRunUtc();
            var delay = nextRun - DateTime.UtcNow;

            _logger.LogInformation("Next monthly rollup scheduled at {NextRun} UTC (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunAsync(stoppingToken);
        }

        _logger.LogInformation("MonthlyRollupJob stopped.");
    }

    private static DateTime ComputeNextRunUtc()
    {
        var now = DateTime.UtcNow;
        var candidate = now.Date.AddMinutes(5); // 00:05 UTC today
        return candidate > now ? candidate : candidate.AddDays(1);
    }

    internal async Task RunAsync(CancellationToken ct, DateOnly? targetDate = null)
    {
        var date = targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        _logger.LogInformation("Starting monthly rollup for {Year}-{Month:D2}", date.Year, date.Month);

        using var scope = _scopeFactory.CreateScope();
        var dailyRepo = scope.ServiceProvider.GetRequiredService<IDailySummaryRepository>();
        var monthlyRepo = scope.ServiceProvider.GetRequiredService<IMonthlySummaryRepository>();
        var cache = scope.ServiceProvider.GetRequiredService<IReportCache>();

        try
        {
            var from = new DateOnly(date.Year, date.Month, 1);
            var to = from.AddMonths(1).AddDays(-1);

            // Guid.Empty retrieves all accounts in the range
            var allDailies = await dailyRepo.GetRangeAsync(Guid.Empty, from, to, ct);
            var accountIds = allDailies.Select(d => d.AccountId).Distinct().ToList();

            _logger.LogInformation("Rolling up {Count} accounts for {Year}-{Month:D2}", accountIds.Count, date.Year, date.Month);

            foreach (var accountId in accountIds)
            {
                await RollupAccountAsync(accountId, date.Year, date.Month, from, to, dailyRepo, monthlyRepo, cache, ct);
            }

            _logger.LogInformation("Monthly rollup completed for {Year}-{Month:D2}", date.Year, date.Month);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monthly rollup failed for {Year}-{Month:D2}", date.Year, date.Month);
        }
    }

    private async Task RollupAccountAsync(
        Guid accountId, int year, int month,
        DateOnly from, DateOnly to,
        IDailySummaryRepository dailyRepo,
        IMonthlySummaryRepository monthlyRepo,
        IReportCache cache,
        CancellationToken ct)
    {
        try
        {
            var accountDailies = await dailyRepo.GetRangeAsync(accountId, from, to, ct);
            if (!accountDailies.Any()) return;

            var prevMonthDate = from.AddMonths(-1);
            var prev = await monthlyRepo.GetAsync(accountId, prevMonthDate.Year, prevMonthDate.Month, ct);
            var openingBalance = prev?.ClosingBalance ?? 0m;

            var monthly = MonthlySummary.ComputeFrom(accountId, year, month, openingBalance, accountDailies);
            await monthlyRepo.UpsertAsync(monthly, ct);

            await cache.RemoveAsync($"monthly:{accountId}:{year}:{month:D2}", ct);

            _logger.LogDebug(
                "Rolled up account {AccountId}: incoming={Incoming} outgoing={Outgoing} net={Net}",
                accountId, monthly.TotalIncoming, monthly.TotalOutgoing, monthly.NetBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to roll up account {AccountId} for {Year}-{Month:D2}", accountId, year, month);
        }
    }
}

using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialBalance.ReportingInfrastructure.Messaging;

/// <summary>
/// Background job that runs at midnight (UTC) to roll up daily summaries into monthly summaries.
/// In production this is replaced by a Kubernetes CronJob; this service handles dev/staging.
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
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Run once per day at 00:05 UTC
            var nextRun = now.Date.AddDays(1).AddMinutes(5);
            var delay = nextRun - now;

            _logger.LogInformation("Monthly rollup job scheduled in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            await RunRollupAsync(yesterday.Year, yesterday.Month, stoppingToken);
        }
    }

    private async Task RunRollupAsync(int year, int month, CancellationToken ct)
    {
        _logger.LogInformation("Starting monthly rollup for {Year}-{Month:D2}", year, month);

        using var scope = _scopeFactory.CreateScope();
        var dailyRepo = scope.ServiceProvider.GetRequiredService<IDailySummaryRepository>();
        var monthlyRepo = scope.ServiceProvider.GetRequiredService<IMonthlySummaryRepository>();
        var cache = scope.ServiceProvider.GetRequiredService<IReportCache>();

        try
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);

            // We need all accounts that have daily summaries in this month.
            // For simplicity we recompute by querying all distinct account IDs from daily summaries.
            var dailies = await dailyRepo.GetRangeAsync(Guid.Empty, from, to, ct);
            var accountIds = dailies.Select(d => d.AccountId).Distinct();

            foreach (var accountId in accountIds)
            {
                var accountDailies = await dailyRepo.GetRangeAsync(accountId, from, to, ct);
                if (!accountDailies.Any()) continue;

                // Opening balance: closing balance of previous month, or 0
                var prevMonth = from.AddMonths(-1);
                var prev = await monthlyRepo.GetAsync(accountId, prevMonth.Year, prevMonth.Month, ct);
                var openingBalance = prev?.ClosingBalance ?? 0m;

                var monthly = MonthlySummary.ComputeFrom(accountId, year, month, openingBalance, accountDailies);
                await monthlyRepo.UpsertAsync(monthly, ct);

                await cache.RemoveAsync($"monthly:{accountId}:{year}:{month:D2}", ct);
                _logger.LogInformation("Rolled up monthly summary for account {AccountId} {Year}-{Month:D2}", accountId, year, month);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monthly rollup failed for {Year}-{Month:D2}", year, month);
        }
    }
}

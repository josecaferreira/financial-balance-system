using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Reporting;
using FinancialBalance.Worker.Jobs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinancialBalance.Worker.Tests.Jobs;

public class MonthlyRollupJobTests
{
    private readonly IDailySummaryRepository _dailyRepo = Substitute.For<IDailySummaryRepository>();
    private readonly IMonthlySummaryRepository _monthlyRepo = Substitute.For<IMonthlySummaryRepository>();
    private readonly IReportCache _cache = Substitute.For<IReportCache>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonthlyRollupJob _job;

    private static readonly Guid AccountId = Guid.NewGuid();

    public MonthlyRollupJobTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_dailyRepo);
        services.AddSingleton(_monthlyRepo);
        services.AddSingleton(_cache);

        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        _job = new MonthlyRollupJob(_scopeFactory, NullLogger<MonthlyRollupJob>.Instance);
    }

    private DailySummary BuildDaily(DateOnly date, decimal incoming, decimal outgoing)
    {
        var summary = DailySummary.Create(AccountId, date);
        if (incoming > 0) summary.ApplyTransaction("Incoming", incoming, "Revenue");
        if (outgoing > 0) summary.ApplyTransaction("Outgoing", outgoing, "Payroll");
        return summary;
    }

    [Fact]
    public async Task RunAsync_WithDailySummaries_CreatesMonthlySummary()
    {
        var targetDate = new DateOnly(2024, 1, 15);
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);

        var dailies = new List<DailySummary>
        {
            BuildDaily(new DateOnly(2024, 1, 1), 5000m, 1000m),
            BuildDaily(new DateOnly(2024, 1, 15), 3000m, 500m)
        };

        // Guid.Empty returns all accounts
        _dailyRepo.GetRangeAsync(Guid.Empty, from, to, Arg.Any<CancellationToken>()).Returns(dailies);
        _dailyRepo.GetRangeAsync(AccountId, from, to, Arg.Any<CancellationToken>()).Returns(dailies);
        _monthlyRepo.GetAsync(AccountId, 2023, 12, Arg.Any<CancellationToken>()).Returns((MonthlySummary?)null);

        await _job.RunAsync(CancellationToken.None, targetDate);

        await _monthlyRepo.Received(1).UpsertAsync(
            Arg.Is<MonthlySummary>(m =>
                m.AccountId == AccountId &&
                m.Year == 2024 &&
                m.Month == 1 &&
                m.TotalIncoming == 8000m &&
                m.TotalOutgoing == 1500m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_UsesOpeningBalanceFromPreviousMonth()
    {
        var targetDate = new DateOnly(2024, 2, 15);
        var from = new DateOnly(2024, 2, 1);
        var to = new DateOnly(2024, 2, 29);

        var prev = MonthlySummary.ComputeFrom(AccountId, 2024, 1, 0m,
            new[] { BuildDaily(new DateOnly(2024, 1, 1), 1000m, 0m) });

        var dailies = new List<DailySummary> { BuildDaily(new DateOnly(2024, 2, 1), 2000m, 0m) };

        _dailyRepo.GetRangeAsync(Guid.Empty, from, to, Arg.Any<CancellationToken>()).Returns(dailies);
        _dailyRepo.GetRangeAsync(AccountId, from, to, Arg.Any<CancellationToken>()).Returns(dailies);
        _monthlyRepo.GetAsync(AccountId, 2024, 1, Arg.Any<CancellationToken>()).Returns(prev);

        await _job.RunAsync(CancellationToken.None, targetDate);

        await _monthlyRepo.Received(1).UpsertAsync(
            Arg.Is<MonthlySummary>(m =>
                m.OpeningBalance == 1000m &&
                m.ClosingBalance == 3000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WithNoDailySummaries_DoesNotUpsert()
    {
        var targetDate = new DateOnly(2024, 1, 15);
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);

        _dailyRepo.GetRangeAsync(Guid.Empty, from, to, Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary>());

        await _job.RunAsync(CancellationToken.None, targetDate);

        await _monthlyRepo.DidNotReceive().UpsertAsync(Arg.Any<MonthlySummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_InvalidatesMonthlyCacheForEachAccount()
    {
        var targetDate = new DateOnly(2024, 1, 15);
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);

        var dailies = new List<DailySummary> { BuildDaily(new DateOnly(2024, 1, 1), 1000m, 0m) };

        _dailyRepo.GetRangeAsync(Guid.Empty, from, to, Arg.Any<CancellationToken>()).Returns(dailies);
        _dailyRepo.GetRangeAsync(AccountId, from, to, Arg.Any<CancellationToken>()).Returns(dailies);
        _monthlyRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((MonthlySummary?)null);

        await _job.RunAsync(CancellationToken.None, targetDate);

        var expectedCacheKey = $"monthly:{AccountId}:2024:01";
        await _cache.Received(1).RemoveAsync(expectedCacheKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenOneAccountFails_ContinuesWithOthers()
    {
        var accountId2 = Guid.NewGuid();
        var targetDate = new DateOnly(2024, 1, 15);
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);

        var daily1 = BuildDaily(new DateOnly(2024, 1, 1), 1000m, 0m);
        var daily2 = DailySummary.Create(accountId2, new DateOnly(2024, 1, 2));
        daily2.ApplyTransaction("Incoming", 500m, "Revenue");

        _dailyRepo.GetRangeAsync(Guid.Empty, from, to, Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary> { daily1, daily2 });

        // First account throws, second should still be processed
        _dailyRepo.GetRangeAsync(AccountId, from, to, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<DailySummary>>(_ => throw new InvalidOperationException("DB error"));
        _dailyRepo.GetRangeAsync(accountId2, from, to, Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary> { daily2 });

        _monthlyRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((MonthlySummary?)null);

        // Should not throw — errors are caught per-account
        await _job.RunAsync(CancellationToken.None, targetDate);

        await _monthlyRepo.Received(1).UpsertAsync(
            Arg.Is<MonthlySummary>(m => m.AccountId == accountId2),
            Arg.Any<CancellationToken>());
    }
}

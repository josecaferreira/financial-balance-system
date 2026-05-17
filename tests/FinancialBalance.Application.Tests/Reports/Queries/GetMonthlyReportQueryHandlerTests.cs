using FinancialBalance.Application.Common;
using FinancialBalance.Application.Reports.Queries.GetMonthlyReport;
using FinancialBalance.Domain.Reporting;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Reports.Queries;

public class GetMonthlyReportQueryHandlerTests
{
    private readonly IMonthlySummaryRepository _monthlyRepo = Substitute.For<IMonthlySummaryRepository>();
    private readonly IDailySummaryRepository _dailyRepo = Substitute.For<IDailySummaryRepository>();
    private readonly IReportCache _cache = Substitute.For<IReportCache>();
    private readonly GetMonthlyReportQueryHandler _handler;

    private static readonly Guid AccountId = Guid.NewGuid();

    public GetMonthlyReportQueryHandlerTests()
        => _handler = new GetMonthlyReportQueryHandler(_monthlyRepo, _dailyRepo, _cache);

    private static MonthlySummary BuildMonthlySummary()
    {
        var daily = DailySummary.Create(AccountId, new DateOnly(2024, 1, 15));
        daily.ApplyTransaction("Incoming", 5000m, "Revenue");
        daily.ApplyTransaction("Outgoing", 1000m, "Payroll");
        return MonthlySummary.ComputeFrom(AccountId, 2024, 1, 2000m, [daily]);
    }

    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedDto()
    {
        var cached = new MonthlyReportDto(AccountId, 2024, 1, 0m, 0m, 0m, 0m, 0m, 0, [], [], DateTime.UtcNow);
        _cache.GetAsync<MonthlyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cached);

        var result = await _handler.Handle(new GetMonthlyReportQuery(AccountId, 2024, 1), CancellationToken.None);

        result.Should().Be(cached);
        await _monthlyRepo.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_ReturnsMonthlyDto()
    {
        _cache.GetAsync<MonthlyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MonthlyReportDto?)null);

        var monthly = BuildMonthlySummary();
        _monthlyRepo.GetAsync(AccountId, 2024, 1, Arg.Any<CancellationToken>()).Returns(monthly);
        _dailyRepo.GetRangeAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary>());

        var result = await _handler.Handle(new GetMonthlyReportQuery(AccountId, 2024, 1), CancellationToken.None);

        result.TotalIncoming.Should().Be(5000m);
        result.TotalOutgoing.Should().Be(1000m);
        result.NetBalance.Should().Be(4000m);
        result.OpeningBalance.Should().Be(2000m);
        result.ClosingBalance.Should().Be(6000m);
    }

    [Fact]
    public async Task Handle_SummaryNotFound_ThrowsNotFoundException()
    {
        _cache.GetAsync<MonthlyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MonthlyReportDto?)null);
        _monthlyRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((MonthlySummary?)null);

        var act = async () => await _handler.Handle(
            new GetMonthlyReportQuery(AccountId, 2024, 1), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CacheKeyContainsAccountIdYearMonth()
    {
        _cache.GetAsync<MonthlyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MonthlyReportDto?)null);

        var monthly = BuildMonthlySummary();
        _monthlyRepo.GetAsync(AccountId, 2024, 1, Arg.Any<CancellationToken>()).Returns(monthly);
        _dailyRepo.GetRangeAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary>());

        await _handler.Handle(new GetMonthlyReportQuery(AccountId, 2024, 1), CancellationToken.None);

        var expectedKey = $"monthly:{AccountId}:2024:01";
        await _cache.Received().GetAsync<MonthlyReportDto>(expectedKey, Arg.Any<CancellationToken>());
    }
}

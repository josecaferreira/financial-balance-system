using FinancialBalance.Application.Common;
using FinancialBalance.Application.Reports.Queries.GetDailyReport;
using FinancialBalance.Domain.Reporting;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Reports.Queries;

public class GetDailyReportQueryHandlerTests
{
    private readonly IDailySummaryRepository _repository = Substitute.For<IDailySummaryRepository>();
    private readonly IReportCache _cache = Substitute.For<IReportCache>();
    private readonly GetDailyReportQueryHandler _handler;

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    public GetDailyReportQueryHandlerTests()
        => _handler = new GetDailyReportQueryHandler(_repository, _cache);

    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedDto_WithoutHittingRepository()
    {
        var cached = new DailyReportDto(AccountId, Yesterday, 1000m, 200m, 800m, 3, [], DateTime.UtcNow);
        _cache.GetAsync<DailyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cached);

        var result = await _handler.Handle(new GetDailyReportQuery(AccountId, Yesterday), CancellationToken.None);

        result.Should().Be(cached);
        await _repository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_QueriesRepositoryAndCachesResult()
    {
        _cache.GetAsync<DailyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DailyReportDto?)null);

        var summary = DailySummary.Create(AccountId, Yesterday);
        summary.ApplyTransaction("Incoming", 1000m, "Revenue");
        _repository.GetAsync(AccountId, Yesterday, Arg.Any<CancellationToken>()).Returns(summary);

        var result = await _handler.Handle(new GetDailyReportQuery(AccountId, Yesterday), CancellationToken.None);

        result.TotalIncoming.Should().Be(1000m);
        result.NetBalance.Should().Be(1000m);
        result.TransactionCount.Should().Be(1);

        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.Contains(AccountId.ToString())),
            Arg.Any<DailyReportDto>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PastDate_CachesFor24Hours()
    {
        _cache.GetAsync<DailyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DailyReportDto?)null);

        var summary = DailySummary.Create(AccountId, Yesterday);
        _repository.GetAsync(AccountId, Yesterday, Arg.Any<CancellationToken>()).Returns(summary);

        await _handler.Handle(new GetDailyReportQuery(AccountId, Yesterday), CancellationToken.None);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<DailyReportDto>(),
            Arg.Is<TimeSpan>(t => t == TimeSpan.FromHours(24)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TodayDate_CachesFor1Hour()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _cache.GetAsync<DailyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DailyReportDto?)null);

        var summary = DailySummary.Create(AccountId, today);
        _repository.GetAsync(AccountId, today, Arg.Any<CancellationToken>()).Returns(summary);

        await _handler.Handle(new GetDailyReportQuery(AccountId, today), CancellationToken.None);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<DailyReportDto>(),
            Arg.Is<TimeSpan>(t => t == TimeSpan.FromHours(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SummaryNotFound_ThrowsNotFoundException()
    {
        _cache.GetAsync<DailyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DailyReportDto?)null);
        _repository.GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var act = async () => await _handler.Handle(
            new GetDailyReportQuery(AccountId, Yesterday), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CacheKeyContainsAccountIdAndDate()
    {
        _cache.GetAsync<DailyReportDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DailyReportDto?)null);

        var summary = DailySummary.Create(AccountId, Yesterday);
        _repository.GetAsync(AccountId, Yesterday, Arg.Any<CancellationToken>()).Returns(summary);

        await _handler.Handle(new GetDailyReportQuery(AccountId, Yesterday), CancellationToken.None);

        var expectedKey = $"daily:{AccountId}:{Yesterday:yyyy-MM-dd}";
        await _cache.Received().GetAsync<DailyReportDto>(expectedKey, Arg.Any<CancellationToken>());
    }
}

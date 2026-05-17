using FinancialBalance.Application.Common;
using FinancialBalance.Application.Reports.Queries.GetDailyReport;
using FinancialBalance.Domain.Reporting;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetMonthlyReport;

public class GetMonthlyReportQueryHandler : IRequestHandler<GetMonthlyReportQuery, MonthlyReportDto>
{
    private readonly IMonthlySummaryRepository _monthlyRepo;
    private readonly IDailySummaryRepository _dailyRepo;
    private readonly IReportCache _cache;

    public GetMonthlyReportQueryHandler(
        IMonthlySummaryRepository monthlyRepo,
        IDailySummaryRepository dailyRepo,
        IReportCache cache)
    {
        _monthlyRepo = monthlyRepo;
        _dailyRepo = dailyRepo;
        _cache = cache;
    }

    public async Task<MonthlyReportDto> Handle(GetMonthlyReportQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"monthly:{request.AccountId}:{request.Year}:{request.Month:D2}";

        var cached = await _cache.GetAsync<MonthlyReportDto>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var monthly = await _monthlyRepo.GetAsync(request.AccountId, request.Year, request.Month, cancellationToken)
            ?? throw new NotFoundException(
                $"No monthly report found for account {request.AccountId} for {request.Year}-{request.Month:D2}.");

        var from = new DateOnly(request.Year, request.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var dailies = await _dailyRepo.GetRangeAsync(request.AccountId, from, to, cancellationToken);

        var dto = ToDto(monthly, dailies);

        var isPastMonth = new DateOnly(request.Year, request.Month, 1) < DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var ttl = isPastMonth ? TimeSpan.FromHours(24) : TimeSpan.FromHours(6);
        await _cache.SetAsync(cacheKey, dto, ttl, cancellationToken);

        return dto;
    }

    internal static MonthlyReportDto ToDto(MonthlySummary m, IEnumerable<DailySummary> dailies)
    {
        var dailyBreakdown = dailies
            .OrderBy(d => d.Date)
            .Select(d => new DailyBreakdownDto(d.Date, d.TotalIncoming, d.TotalOutgoing, d.NetBalance))
            .ToList();

        var categoryBreakdown = m.CategoryBreakdowns
            .Select(c => new CategoryBreakdownDto(c.Category, c.TotalIncoming, c.TotalOutgoing))
            .ToList();

        return new MonthlyReportDto(
            m.AccountId, m.Year, m.Month,
            m.OpeningBalance, m.ClosingBalance,
            m.TotalIncoming, m.TotalOutgoing, m.NetBalance,
            m.TransactionCount,
            dailyBreakdown, categoryBreakdown,
            m.ComputedAt);
    }
}

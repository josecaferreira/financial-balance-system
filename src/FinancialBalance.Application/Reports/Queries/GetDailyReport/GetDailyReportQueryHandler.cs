using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Reporting;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetDailyReport;

public class GetDailyReportQueryHandler : IRequestHandler<GetDailyReportQuery, DailyReportDto>
{
    private readonly IDailySummaryRepository _repository;
    private readonly IReportCache _cache;

    public GetDailyReportQueryHandler(IDailySummaryRepository repository, IReportCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<DailyReportDto> Handle(GetDailyReportQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"daily:{request.AccountId}:{request.Date:yyyy-MM-dd}";

        var cached = await _cache.GetAsync<DailyReportDto>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var summary = await _repository.GetAsync(request.AccountId, request.Date, cancellationToken)
            ?? throw new NotFoundException($"No daily report found for account {request.AccountId} on {request.Date}.");

        var dto = ToDto(summary);

        var isPastDate = request.Date < DateOnly.FromDateTime(DateTime.UtcNow);
        var ttl = isPastDate ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1);
        await _cache.SetAsync(cacheKey, dto, ttl, cancellationToken);

        return dto;
    }

    internal static DailyReportDto ToDto(DailySummary s) => new(
        s.AccountId,
        s.Date,
        s.TotalIncoming,
        s.TotalOutgoing,
        s.NetBalance,
        s.TransactionCount,
        s.CategoryBreakdowns.Select(c => new CategoryBreakdownDto(c.Category, c.TotalIncoming, c.TotalOutgoing)).ToList(),
        s.ComputedAt);
}

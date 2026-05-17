using FinancialBalance.Application.Reports.Queries.GetDailyReport;
using FinancialBalance.Domain.Reporting;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetDailyReportRange;

public class GetDailyReportRangeQueryHandler : IRequestHandler<GetDailyReportRangeQuery, IReadOnlyList<DailyReportDto>>
{
    private readonly IDailySummaryRepository _repository;

    public GetDailyReportRangeQueryHandler(IDailySummaryRepository repository)
        => _repository = repository;

    public async Task<IReadOnlyList<DailyReportDto>> Handle(GetDailyReportRangeQuery request, CancellationToken cancellationToken)
    {
        var summaries = await _repository.GetRangeAsync(request.AccountId, request.From, request.To, cancellationToken);
        return summaries.Select(GetDailyReportQueryHandler.ToDto).ToList();
    }
}

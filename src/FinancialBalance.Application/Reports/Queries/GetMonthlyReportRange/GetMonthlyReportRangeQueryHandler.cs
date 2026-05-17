using FinancialBalance.Domain.Reporting;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetMonthlyReportRange;

public class GetMonthlyReportRangeQueryHandler : IRequestHandler<GetMonthlyReportRangeQuery, IReadOnlyList<MonthlyReportSummaryDto>>
{
    private readonly IMonthlySummaryRepository _repository;

    public GetMonthlyReportRangeQueryHandler(IMonthlySummaryRepository repository)
        => _repository = repository;

    public async Task<IReadOnlyList<MonthlyReportSummaryDto>> Handle(
        GetMonthlyReportRangeQuery request, CancellationToken cancellationToken)
    {
        var summaries = await _repository.GetRangeAsync(
            request.AccountId,
            request.FromYear, request.FromMonth,
            request.ToYear, request.ToMonth,
            cancellationToken);

        return summaries
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .Select(m => new MonthlyReportSummaryDto(
                m.AccountId, m.Year, m.Month,
                m.OpeningBalance, m.ClosingBalance,
                m.TotalIncoming, m.TotalOutgoing, m.NetBalance,
                m.TransactionCount))
            .ToList();
    }
}

using FinancialBalance.Application.Reports.Queries.GetMonthlyReport;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetMonthlyReportRange;

public record GetMonthlyReportRangeQuery(
    Guid AccountId,
    int FromYear, int FromMonth,
    int ToYear, int ToMonth) : IRequest<IReadOnlyList<MonthlyReportSummaryDto>>;

public record MonthlyReportSummaryDto(
    Guid AccountId,
    int Year,
    int Month,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalIncoming,
    decimal TotalOutgoing,
    decimal NetBalance,
    int TransactionCount);

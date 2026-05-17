using FinancialBalance.Application.Reports.Queries.GetDailyReport;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetMonthlyReport;

public record GetMonthlyReportQuery(Guid AccountId, int Year, int Month) : IRequest<MonthlyReportDto>;

public record MonthlyReportDto(
    Guid AccountId,
    int Year,
    int Month,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalIncoming,
    decimal TotalOutgoing,
    decimal NetBalance,
    int TransactionCount,
    IReadOnlyList<DailyBreakdownDto> DailyBreakdown,
    IReadOnlyList<CategoryBreakdownDto> CategoryBreakdown,
    DateTime ComputedAt);

public record DailyBreakdownDto(DateOnly Date, decimal Incoming, decimal Outgoing, decimal Net);

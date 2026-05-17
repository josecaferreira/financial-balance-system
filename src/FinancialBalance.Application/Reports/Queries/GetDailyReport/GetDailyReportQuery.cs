using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetDailyReport;

public record GetDailyReportQuery(Guid AccountId, DateOnly Date) : IRequest<DailyReportDto>;

public record DailyReportDto(
    Guid AccountId,
    DateOnly Date,
    decimal TotalIncoming,
    decimal TotalOutgoing,
    decimal NetBalance,
    int TransactionCount,
    IReadOnlyList<CategoryBreakdownDto> Breakdown,
    DateTime ComputedAt);

public record CategoryBreakdownDto(string Category, decimal Incoming, decimal Outgoing);

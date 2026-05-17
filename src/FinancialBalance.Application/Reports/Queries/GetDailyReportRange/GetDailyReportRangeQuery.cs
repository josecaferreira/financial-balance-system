using FinancialBalance.Application.Reports.Queries.GetDailyReport;
using MediatR;

namespace FinancialBalance.Application.Reports.Queries.GetDailyReportRange;

public record GetDailyReportRangeQuery(Guid AccountId, DateOnly From, DateOnly To) : IRequest<IReadOnlyList<DailyReportDto>>;

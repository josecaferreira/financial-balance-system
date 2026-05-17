using FluentValidation;

namespace FinancialBalance.Application.Reports.Queries.GetMonthlyReport;

public class GetMonthlyReportQueryValidator : AbstractValidator<GetMonthlyReportQuery>
{
    public GetMonthlyReportQueryValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

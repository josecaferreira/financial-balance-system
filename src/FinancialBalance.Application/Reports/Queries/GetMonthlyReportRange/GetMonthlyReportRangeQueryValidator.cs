using FluentValidation;

namespace FinancialBalance.Application.Reports.Queries.GetMonthlyReportRange;

public class GetMonthlyReportRangeQueryValidator : AbstractValidator<GetMonthlyReportRangeQuery>
{
    public GetMonthlyReportRangeQueryValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.FromYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.FromMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ToYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.ToMonth).InclusiveBetween(1, 12);

        RuleFor(x => x)
            .Must(x =>
            {
                var from = x.FromYear * 12 + x.FromMonth;
                var to = x.ToYear * 12 + x.ToMonth;
                return from <= to && (to - from) <= 11;
            })
            .WithMessage("Range must be valid and cannot exceed 12 months.")
            .OverridePropertyName("range");
    }
}

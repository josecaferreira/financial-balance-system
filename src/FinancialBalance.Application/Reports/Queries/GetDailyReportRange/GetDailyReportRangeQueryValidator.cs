using FluentValidation;

namespace FinancialBalance.Application.Reports.Queries.GetDailyReportRange;

public class GetDailyReportRangeQueryValidator : AbstractValidator<GetDailyReportRangeQuery>
{
    public GetDailyReportRangeQueryValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();

        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To)
            .WithMessage("'From' must be before or equal to 'To'.");

        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber <= 92)
            .WithMessage("Date range cannot exceed 92 days.")
            .OverridePropertyName("range");
    }
}

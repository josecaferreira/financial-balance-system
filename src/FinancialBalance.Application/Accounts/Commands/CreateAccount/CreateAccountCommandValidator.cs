using FluentValidation;

namespace FinancialBalance.Application.Accounts.Commands.CreateAccount;

public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^[A-Za-z0-9\-_]+$")
            .WithMessage("Code may only contain letters, numbers, hyphens, and underscores.");

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Currency)
            .IsInEnum();
    }
}

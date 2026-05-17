using FluentValidation;

namespace FinancialBalance.Application.Transactions.Commands.CreateTransaction;

public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999.99m)
            .WithMessage("Amount must be between 0.01 and 999,999,999.99.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Category)
            .IsInEnum();

        RuleFor(x => x.TransactionDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Transaction date cannot be in the future.");

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(100)
            .When(x => x.ReferenceNumber is not null);
    }
}

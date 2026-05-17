using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace FinancialBalance.Application.Tests.Transactions.Commands;

public class CreateTransactionCommandValidatorTests
{
    private readonly CreateTransactionCommandValidator _validator = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static CreateTransactionCommand ValidCommand() => new(
        Guid.NewGuid(), TransactionType.Incoming, 100m, "Description",
        TransactionCategory.Revenue, Today, null);

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyAccountId_HasError()
    {
        var cmd = ValidCommand() with { AccountId = Guid.Empty };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.AccountId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Validate_NonPositiveAmount_HasError(decimal amount)
    {
        var cmd = ValidCommand() with { Amount = amount };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_AmountExceedsMax_HasError()
    {
        var cmd = ValidCommand() with { Amount = 1_000_000_000m };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_EmptyDescription_HasError()
    {
        var cmd = ValidCommand() with { Description = "" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_DescriptionTooLong_HasError()
    {
        var cmd = ValidCommand() with { Description = new string('x', 501) };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_FutureTransactionDate_HasError()
    {
        var cmd = ValidCommand() with { TransactionDate = Today.AddDays(1) };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TransactionDate);
    }

    [Fact]
    public void Validate_ReferenceNumberTooLong_HasError()
    {
        var cmd = ValidCommand() with { ReferenceNumber = new string('x', 101) };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ReferenceNumber);
    }

    [Fact]
    public void Validate_NullReferenceNumber_HasNoError()
    {
        var cmd = ValidCommand() with { ReferenceNumber = null };
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.ReferenceNumber);
    }
}

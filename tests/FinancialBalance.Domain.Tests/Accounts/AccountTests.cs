using FinancialBalance.Domain.Accounts;
using FinancialBalance.Domain.Accounts.Events;
using FluentAssertions;

namespace FinancialBalance.Domain.Tests.Accounts;

public class AccountTests
{
    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidInputs_ReturnsActiveAccountWithZeroBalance()
    {
        var account = Account.Create("Main Account", "MAIN-001", AccountType.Checking, Currency.BRL);

        account.Name.Should().Be("Main Account");
        account.Code.Should().Be("MAIN-001");
        account.Type.Should().Be(AccountType.Checking);
        account.Currency.Should().Be(Currency.BRL);
        account.CurrentBalance.Should().Be(0m);
        account.IsActive.Should().BeTrue();
        account.Transactions.Should().BeEmpty();
    }

    [Fact]
    public void Create_NormalizesCodeToUppercase()
    {
        var account = Account.Create("Test", "lower-code", AccountType.Savings, Currency.USD);
        account.Code.Should().Be("LOWER-CODE");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsDomainException(string name)
    {
        var act = () => Account.Create(name, "CODE", AccountType.Checking, Currency.BRL);
        act.Should().Throw<DomainException>().WithMessage("*name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCode_ThrowsDomainException(string code)
    {
        var act = () => Account.Create("Name", code, AccountType.Checking, Currency.BRL);
        act.Should().Throw<DomainException>().WithMessage("*code*");
    }

    // ── RegisterTransaction ───────────────────────────────────────────────

    [Fact]
    public void RegisterTransaction_Incoming_IncreasesBalance()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        account.RegisterTransaction(TransactionType.Incoming, 1000m, "Payment", TransactionCategory.Revenue, today, userId);

        account.CurrentBalance.Should().Be(1000m);
        account.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public void RegisterTransaction_Outgoing_DecreasesBalance()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        account.RegisterTransaction(TransactionType.Incoming, 2000m, "Revenue", TransactionCategory.Revenue, today, userId);
        account.RegisterTransaction(TransactionType.Outgoing, 500m, "Rent", TransactionCategory.Rent, today, userId);

        account.CurrentBalance.Should().Be(1500m);
    }

    [Fact]
    public void RegisterTransaction_RaisesTransactionCreatedEvent()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        account.DomainEvents.Should().ContainSingle(e => e is TransactionCreated);
    }

    [Fact]
    public void RegisterTransaction_RaisesAccountBalanceUpdatedEvent()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        account.DomainEvents.Should().ContainSingle(e => e is AccountBalanceUpdated)
            .Which.As<AccountBalanceUpdated>().NewBalance.Should().Be(100m);
    }

    [Fact]
    public void RegisterTransaction_OnInactiveAccount_ThrowsDomainException()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        account.Deactivate();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var act = () => account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*inactive*");
    }

    [Fact]
    public void RegisterTransaction_WithFutureDate_ThrowsDomainException()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var act = () => account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, tomorrow, Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void RegisterTransaction_WithZeroAmount_ThrowsDomainException()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var act = () => account.RegisterTransaction(TransactionType.Incoming, 0m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*Amount*");
    }

    // ── CancelTransaction ────────────────────────────────────────────────

    [Fact]
    public void CancelTransaction_Incoming_ReversesBalance()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = Guid.NewGuid();

        var tx = account.RegisterTransaction(TransactionType.Incoming, 1000m, "Revenue", TransactionCategory.Revenue, today, userId);
        account.ClearDomainEvents();

        account.CancelTransaction(tx.Id);

        account.CurrentBalance.Should().Be(0m);
        account.DomainEvents.Should().ContainSingle(e => e is TransactionCancelled);
    }

    [Fact]
    public void CancelTransaction_Outgoing_ReversesBalance()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = Guid.NewGuid();

        account.RegisterTransaction(TransactionType.Incoming, 1000m, "Revenue", TransactionCategory.Revenue, today, userId);
        var tx = account.RegisterTransaction(TransactionType.Outgoing, 300m, "Rent", TransactionCategory.Rent, today, userId);
        account.ClearDomainEvents();

        account.CancelTransaction(tx.Id);

        account.CurrentBalance.Should().Be(1000m);
    }

    [Fact]
    public void CancelTransaction_AlreadyCancelled_ThrowsDomainException()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tx = account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        account.CancelTransaction(tx.Id);

        var act = () => account.CancelTransaction(tx.Id);
        act.Should().Throw<DomainException>().WithMessage("*already cancelled*");
    }

    [Fact]
    public void CancelTransaction_UnknownId_ThrowsDomainException()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);

        var act = () => account.CancelTransaction(Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("*not found*");
    }

    // ── Deactivate ────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_WithNoPendingTransactions_SetsInactive()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);

        account.Deactivate();

        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        account.ClearDomainEvents();

        account.DomainEvents.Should().BeEmpty();
    }
}

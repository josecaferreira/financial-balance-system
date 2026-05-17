using FinancialBalance.Domain.Accounts;
using FluentAssertions;

namespace FinancialBalance.Domain.Tests.Accounts;

public class TransactionTests
{
    private static Account BuildAccount() =>
        Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void RegisterTransaction_SetsStatusToConfirmed()
    {
        var account = BuildAccount();

        var tx = account.RegisterTransaction(TransactionType.Incoming, 500m, "Desc", TransactionCategory.Revenue, Today, Guid.NewGuid());

        tx.Status.Should().Be(TransactionStatus.Confirmed);
    }

    [Fact]
    public void RegisterTransaction_StoresReferenceNumber()
    {
        var account = BuildAccount();

        var tx = account.RegisterTransaction(TransactionType.Incoming, 500m, "Desc", TransactionCategory.Revenue, Today, Guid.NewGuid(), "INV-001");

        tx.ReferenceNumber.Should().Be("INV-001");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void RegisterTransaction_NonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var account = BuildAccount();

        var act = () => account.RegisterTransaction(TransactionType.Incoming, amount, "Desc", TransactionCategory.Revenue, Today, Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_ChangesStatusToCancelled()
    {
        var account = BuildAccount();
        var tx = account.RegisterTransaction(TransactionType.Incoming, 100m, "Desc", TransactionCategory.Revenue, Today, Guid.NewGuid());

        account.CancelTransaction(tx.Id);

        account.Transactions.First(t => t.Id == tx.Id).Status.Should().Be(TransactionStatus.Cancelled);
    }
}

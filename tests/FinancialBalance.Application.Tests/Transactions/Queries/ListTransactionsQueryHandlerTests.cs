using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Queries.ListTransactions;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Transactions.Queries;

public class ListTransactionsQueryHandlerTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly ListTransactionsQueryHandler _handler;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public ListTransactionsQueryHandlerTests()
        => _handler = new ListTransactionsQueryHandler(_repository);

    private Account BuildAccountWithTransactions()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var userId = Guid.NewGuid();
        account.RegisterTransaction(TransactionType.Incoming, 1000m, "Revenue 1", TransactionCategory.Revenue, Today, userId);
        account.RegisterTransaction(TransactionType.Outgoing, 300m, "Supplier 1", TransactionCategory.Supplier, Today, userId);
        account.RegisterTransaction(TransactionType.Incoming, 500m, "Revenue 2", TransactionCategory.Revenue, Today.AddDays(-1), userId);
        return account;
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllTransactions()
    {
        var account = BuildAccountWithTransactions();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var query = new ListTransactionsQuery(account.Id, null, null, null, null, null, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Data.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_FilterByType_ReturnsOnlyMatchingTransactions()
    {
        var account = BuildAccountWithTransactions();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var query = new ListTransactionsQuery(account.Id, TransactionType.Incoming, null, null, null, null, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Data.Should().HaveCount(2);
        result.Data.Should().AllSatisfy(t => t.Type.Should().Be(TransactionType.Incoming));
    }

    [Fact]
    public async Task Handle_FilterByCategory_ReturnsOnlyMatchingTransactions()
    {
        var account = BuildAccountWithTransactions();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var query = new ListTransactionsQuery(account.Id, null, TransactionCategory.Supplier, null, null, null, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Data.Should().HaveCount(1);
        result.Data.First().Category.Should().Be(TransactionCategory.Supplier);
    }

    [Fact]
    public async Task Handle_FilterByDateRange_ReturnsOnlyMatchingTransactions()
    {
        var account = BuildAccountWithTransactions();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var query = new ListTransactionsQuery(account.Id, null, null, null, Today, Today, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Data.Should().HaveCount(2);
        result.Data.Should().AllSatisfy(t => t.TransactionDate.Should().Be(Today));
    }

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectPage()
    {
        var account = BuildAccountWithTransactions();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var query = new ListTransactionsQuery(account.Id, null, null, null, null, null, 1, 2);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Data.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var act = async () => await _handler.Handle(
            new ListTransactionsQuery(Guid.NewGuid(), null, null, null, null, null, 1, 20), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

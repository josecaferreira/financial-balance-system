using FinancialBalance.Application.Accounts.Queries.GetAccountBalance;
using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Accounts.Queries;

public class GetAccountBalanceQueryHandlerTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly GetAccountBalanceQueryHandler _handler;

    public GetAccountBalanceQueryHandlerTests()
        => _handler = new GetAccountBalanceQueryHandler(_repository);

    [Fact]
    public async Task Handle_ReturnsCurrentBalance()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        account.RegisterTransaction(TransactionType.Incoming, 500m, "Desc", TransactionCategory.Revenue, today, Guid.NewGuid());

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _handler.Handle(new GetAccountBalanceQuery(account.Id), CancellationToken.None);

        result.CurrentBalance.Should().Be(500m);
        result.Currency.Should().Be("BRL");
    }

    [Fact]
    public async Task Handle_MissingAccount_ThrowsNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var act = async () => await _handler.Handle(new GetAccountBalanceQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

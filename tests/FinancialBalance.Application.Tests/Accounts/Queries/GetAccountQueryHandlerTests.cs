using FinancialBalance.Application.Accounts.Queries.GetAccount;
using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Accounts.Queries;

public class GetAccountQueryHandlerTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly GetAccountQueryHandler _handler;

    public GetAccountQueryHandlerTests()
        => _handler = new GetAccountQueryHandler(_repository);

    [Fact]
    public async Task Handle_ExistingAccount_ReturnsDto()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _handler.Handle(new GetAccountQuery(account.Id), CancellationToken.None);

        result.Id.Should().Be(account.Id);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task Handle_MissingAccount_ThrowsNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var act = async () => await _handler.Handle(new GetAccountQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

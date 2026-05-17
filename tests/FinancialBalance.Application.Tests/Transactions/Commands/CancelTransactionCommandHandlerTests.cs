using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Commands.CancelTransaction;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Transactions.Commands;

public class CancelTransactionCommandHandlerTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly CancelTransactionCommandHandler _handler;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public CancelTransactionCommandHandlerTests()
        => _handler = new CancelTransactionCommandHandler(_repository);

    [Fact]
    public async Task Handle_ValidCommand_CancelsTransaction()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        var tx = account.RegisterTransaction(TransactionType.Incoming, 500m, "Desc", TransactionCategory.Revenue, Today, Guid.NewGuid());
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await _handler.Handle(new CancelTransactionCommand(account.Id, tx.Id), CancellationToken.None);

        account.Transactions.First(t => t.Id == tx.Id).Status.Should().Be(TransactionStatus.Cancelled);
        _repository.Received(1).Update(account);
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var act = async () => await _handler.Handle(
            new CancelTransactionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_TransactionNotFound_ThrowsDomainException()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var act = async () => await _handler.Handle(
            new CancelTransactionCommand(account.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<Domain.Accounts.DomainException>().WithMessage("*not found*");
    }
}

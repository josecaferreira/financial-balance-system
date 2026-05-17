using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Transactions.Commands;

public class CreateTransactionCommandHandlerTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly CreateTransactionCommandHandler _handler;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public CreateTransactionCommandHandlerTests()
    {
        _currentUser.Id.Returns(Guid.NewGuid());
        _handler = new CreateTransactionCommandHandler(_repository, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsTransactionDto()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var command = new CreateTransactionCommand(
            account.Id, TransactionType.Incoming, 1000m,
            "Client payment", TransactionCategory.Revenue, Today, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Amount.Should().Be(1000m);
        result.Type.Should().Be(TransactionType.Incoming);
        result.Status.Should().Be(TransactionStatus.Confirmed);
        result.AccountId.Should().Be(account.Id);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsRepositoryUpdate()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var command = new CreateTransactionCommand(
            account.Id, TransactionType.Outgoing, 200m,
            "Supplier", TransactionCategory.Supplier, Today, "REF-001");

        await _handler.Handle(command, CancellationToken.None);

        _repository.Received(1).Update(account);
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var command = new CreateTransactionCommand(
            Guid.NewGuid(), TransactionType.Incoming, 100m,
            "Desc", TransactionCategory.Revenue, Today, null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_IncomingTransaction_IncreasesAccountBalance()
    {
        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var command = new CreateTransactionCommand(
            account.Id, TransactionType.Incoming, 750m,
            "Revenue", TransactionCategory.Revenue, Today, null);

        await _handler.Handle(command, CancellationToken.None);

        account.CurrentBalance.Should().Be(750m);
    }

    [Fact]
    public async Task Handle_UsesCurrentUserIdAsCreatedBy()
    {
        var userId = Guid.NewGuid();
        _currentUser.Id.Returns(userId);

        var account = Account.Create("Test", "T-001", AccountType.Checking, Currency.BRL);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var command = new CreateTransactionCommand(
            account.Id, TransactionType.Incoming, 100m,
            "Desc", TransactionCategory.Revenue, Today, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        account.Transactions.Should().ContainSingle(t => t.Id == result.Id);
    }
}

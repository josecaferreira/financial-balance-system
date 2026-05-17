using FinancialBalance.Application.Accounts.Commands.CreateAccount;
using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinancialBalance.Application.Tests.Accounts.Commands;

public class CreateAccountCommandHandlerTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly CreateAccountCommandHandler _handler;

    public CreateAccountCommandHandlerTests()
        => _handler = new CreateAccountCommandHandler(_repository);

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAndReturnsAccount()
    {
        _repository.ExistsByCodeAsync("MAIN-001", Arg.Any<CancellationToken>()).Returns(false);

        var command = new CreateAccountCommand("Main Account", "MAIN-001", AccountType.Checking, Currency.BRL);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Main Account");
        result.Code.Should().Be("MAIN-001");
        result.CurrentBalance.Should().Be(0m);
        result.IsActive.Should().BeTrue();

        await _repository.Received(1).AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateCode_ThrowsConflictException()
    {
        _repository.ExistsByCodeAsync("DUPE", Arg.Any<CancellationToken>()).Returns(true);

        var command = new CreateAccountCommand("Account", "DUPE", AccountType.Checking, Currency.BRL);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*DUPE*");
    }

    [Fact]
    public async Task Handle_DoesNotAddAccount_WhenCodeAlreadyExists()
    {
        _repository.ExistsByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var command = new CreateAccountCommand("Account", "DUPE", AccountType.Checking, Currency.BRL);

        try { await _handler.Handle(command, CancellationToken.None); } catch { }

        await _repository.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }
}

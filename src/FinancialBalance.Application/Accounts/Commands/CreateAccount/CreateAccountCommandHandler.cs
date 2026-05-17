using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Accounts.Commands.CreateAccount;

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, AccountDto>
{
    private readonly IAccountRepository _repository;

    public CreateAccountCommandHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task<AccountDto> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var exists = await _repository.ExistsByCodeAsync(request.Code, cancellationToken);
        if (exists)
            throw new ConflictException($"An account with code '{request.Code}' already exists.");

        var account = Account.Create(request.Name, request.Code, request.Type, request.Currency);
        await _repository.AddAsync(account, cancellationToken);

        return ToDto(account);
    }

    internal static AccountDto ToDto(Account a) => new(
        a.Id, a.Name, a.Code, a.Type, a.Currency, a.CurrentBalance, a.IsActive, a.CreatedAt);
}

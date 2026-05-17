using FinancialBalance.Application.Accounts.Commands.CreateAccount;
using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Accounts.Queries.GetAccount;

public class GetAccountQueryHandler : IRequestHandler<GetAccountQuery, AccountDto>
{
    private readonly IAccountRepository _repository;

    public GetAccountQueryHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task<AccountDto> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} not found.");

        return CreateAccountCommandHandler.ToDto(account);
    }
}

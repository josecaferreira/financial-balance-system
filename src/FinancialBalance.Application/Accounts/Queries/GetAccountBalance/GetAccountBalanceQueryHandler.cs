using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Accounts.Queries.GetAccountBalance;

public class GetAccountBalanceQueryHandler : IRequestHandler<GetAccountBalanceQuery, AccountBalanceDto>
{
    private readonly IAccountRepository _repository;

    public GetAccountBalanceQueryHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task<AccountBalanceDto> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} not found.");

        return new AccountBalanceDto(
            account.Id,
            account.CurrentBalance,
            account.Currency.ToString(),
            DateTime.UtcNow);
    }
}

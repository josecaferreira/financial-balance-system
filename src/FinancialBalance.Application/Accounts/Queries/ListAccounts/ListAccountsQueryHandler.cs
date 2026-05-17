using FinancialBalance.Application.Accounts.Commands.CreateAccount;
using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Accounts.Queries.ListAccounts;

public class ListAccountsQueryHandler : IRequestHandler<ListAccountsQuery, PagedResult<AccountDto>>
{
    private readonly IAccountRepository _repository;

    public ListAccountsQueryHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task<PagedResult<AccountDto>> Handle(ListAccountsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.ListAsync(request.IsActive, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateAccountCommandHandler.ToDto).ToList();
        return new PagedResult<AccountDto>(dtos, request.Page, request.PageSize, total);
    }
}

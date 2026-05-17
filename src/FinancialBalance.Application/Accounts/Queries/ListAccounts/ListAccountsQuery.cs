using FinancialBalance.Application.Accounts.Commands.CreateAccount;
using FinancialBalance.Application.Common;
using MediatR;

namespace FinancialBalance.Application.Accounts.Queries.ListAccounts;

public record ListAccountsQuery(bool? IsActive, int Page, int PageSize) : IRequest<PagedResult<AccountDto>>;

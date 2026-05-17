using FinancialBalance.Application.Accounts.Commands.CreateAccount;
using MediatR;

namespace FinancialBalance.Application.Accounts.Queries.GetAccount;

public record GetAccountQuery(Guid AccountId) : IRequest<AccountDto>;

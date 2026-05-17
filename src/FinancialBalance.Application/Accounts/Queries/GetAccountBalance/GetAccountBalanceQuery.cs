using MediatR;

namespace FinancialBalance.Application.Accounts.Queries.GetAccountBalance;

public record GetAccountBalanceQuery(Guid AccountId) : IRequest<AccountBalanceDto>;

public record AccountBalanceDto(
    Guid AccountId,
    decimal CurrentBalance,
    string Currency,
    DateTime AsOf);

using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Accounts.Commands.CreateAccount;

public record CreateAccountCommand(
    string Name,
    string Code,
    AccountType Type,
    Currency Currency) : IRequest<AccountDto>;

public record AccountDto(
    Guid Id,
    string Name,
    string Code,
    AccountType Type,
    Currency Currency,
    decimal CurrentBalance,
    bool IsActive,
    DateTime CreatedAt);

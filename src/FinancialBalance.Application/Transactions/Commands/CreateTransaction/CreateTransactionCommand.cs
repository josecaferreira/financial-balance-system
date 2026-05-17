using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Transactions.Commands.CreateTransaction;

public record CreateTransactionCommand(
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    string Description,
    TransactionCategory Category,
    DateOnly TransactionDate,
    string? ReferenceNumber) : IRequest<TransactionDto>;

public record TransactionDto(
    Guid Id,
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    string Description,
    TransactionCategory Category,
    string? ReferenceNumber,
    TransactionStatus Status,
    DateOnly TransactionDate,
    DateTime CreatedAt);

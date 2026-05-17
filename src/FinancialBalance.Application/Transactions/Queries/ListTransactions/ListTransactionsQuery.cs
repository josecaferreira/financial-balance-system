using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Transactions.Queries.ListTransactions;

public record ListTransactionsQuery(
    Guid AccountId,
    TransactionType? Type,
    TransactionCategory? Category,
    TransactionStatus? Status,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize) : IRequest<PagedResult<TransactionDto>>;

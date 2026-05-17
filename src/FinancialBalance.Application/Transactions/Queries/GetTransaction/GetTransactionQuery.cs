using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using MediatR;

namespace FinancialBalance.Application.Transactions.Queries.GetTransaction;

public record GetTransactionQuery(Guid AccountId, Guid TransactionId) : IRequest<TransactionDto>;

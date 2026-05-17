using MediatR;

namespace FinancialBalance.Application.Transactions.Commands.CancelTransaction;

public record CancelTransactionCommand(Guid AccountId, Guid TransactionId) : IRequest;

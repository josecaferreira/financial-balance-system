using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Transactions.Queries.GetTransaction;

public class GetTransactionQueryHandler : IRequestHandler<GetTransactionQuery, TransactionDto>
{
    private readonly IAccountRepository _repository;

    public GetTransactionQueryHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task<TransactionDto> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} not found.");

        var transaction = account.Transactions.FirstOrDefault(t => t.Id == request.TransactionId)
            ?? throw new NotFoundException($"Transaction {request.TransactionId} not found.");

        return CreateTransactionCommandHandler.ToDto(transaction);
    }
}

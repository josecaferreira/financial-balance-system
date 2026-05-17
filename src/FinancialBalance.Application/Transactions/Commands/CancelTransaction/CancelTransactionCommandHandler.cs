using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Transactions.Commands.CancelTransaction;

public class CancelTransactionCommandHandler : IRequestHandler<CancelTransactionCommand>
{
    private readonly IAccountRepository _repository;

    public CancelTransactionCommandHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task Handle(CancelTransactionCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} not found.");

        account.CancelTransaction(request.TransactionId);
        _repository.Update(account);
    }
}

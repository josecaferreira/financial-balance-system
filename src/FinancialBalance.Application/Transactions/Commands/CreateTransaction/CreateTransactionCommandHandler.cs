using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Transactions.Commands.CreateTransaction;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly IAccountRepository _repository;
    private readonly ICurrentUser _currentUser;

    public CreateTransactionCommandHandler(IAccountRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} not found.");

        var transaction = account.RegisterTransaction(
            request.Type,
            request.Amount,
            request.Description,
            request.Category,
            request.TransactionDate,
            _currentUser.Id,
            request.ReferenceNumber);

        _repository.Update(account);

        return ToDto(transaction);
    }

    internal static TransactionDto ToDto(Domain.Accounts.Transaction t) => new(
        t.Id, t.AccountId, t.Type, t.Amount, t.Description,
        t.Category, t.ReferenceNumber, t.Status, t.TransactionDate, t.CreatedAt);
}

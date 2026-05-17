using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using FinancialBalance.Domain.Accounts;
using MediatR;

namespace FinancialBalance.Application.Transactions.Queries.ListTransactions;

public class ListTransactionsQueryHandler : IRequestHandler<ListTransactionsQuery, PagedResult<TransactionDto>>
{
    private readonly IAccountRepository _repository;

    public ListTransactionsQueryHandler(IAccountRepository repository)
        => _repository = repository;

    public async Task<PagedResult<TransactionDto>> Handle(ListTransactionsQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} not found.");

        var query = account.Transactions.AsQueryable();

        if (request.Type.HasValue)
            query = query.Where(t => t.Type == request.Type.Value);

        if (request.Category.HasValue)
            query = query.Where(t => t.Category == request.Category.Value);

        if (request.Status.HasValue)
            query = query.Where(t => t.Status == request.Status.Value);

        if (request.From.HasValue)
            query = query.Where(t => t.TransactionDate >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(t => t.TransactionDate <= request.To.Value);

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(CreateTransactionCommandHandler.ToDto)
            .ToList();

        return new PagedResult<TransactionDto>(items, request.Page, request.PageSize, totalCount);
    }
}

using FinancialBalance.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FinancialBalance.Infrastructure.Persistence.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
        => _context = context;

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Accounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Account?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == code.ToUpperInvariant(), ct);

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
        => await _context.Accounts
            .AnyAsync(a => a.Code == code.ToUpperInvariant(), ct);

    public async Task AddAsync(Account entity, CancellationToken ct = default)
        => await _context.Accounts.AddAsync(entity, ct);

    public void Update(Account entity)
        => _context.Accounts.Update(entity);

    public async Task<(IReadOnlyList<Account> Items, int TotalCount)> ListAsync(
        bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Accounts.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

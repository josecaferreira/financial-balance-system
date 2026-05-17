using FinancialBalance.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FinancialBalance.Worker.Infrastructure.Persistence.Repositories;

public class DailySummaryRepository : IDailySummaryRepository
{
    private readonly WorkerDbContext _context;

    public DailySummaryRepository(WorkerDbContext context)
        => _context = context;

    public async Task<DailySummary?> GetAsync(Guid accountId, DateOnly date, CancellationToken ct = default)
        => await _context.DailySummaries
            .Include("CategoryBreakdowns")
            .FirstOrDefaultAsync(d => d.AccountId == accountId && d.Date == date, ct);

    public async Task<IReadOnlyList<DailySummary>> GetRangeAsync(
        Guid accountId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var query = _context.DailySummaries.Include("CategoryBreakdowns");

        // Guid.Empty = wildcard: return all accounts in range (used by rollup job)
        if (accountId == Guid.Empty)
            return await query
                .Where(d => d.Date >= from && d.Date <= to)
                .OrderBy(d => d.AccountId).ThenBy(d => d.Date)
                .ToListAsync(ct);

        return await query
            .Where(d => d.AccountId == accountId && d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(DailySummary summary, CancellationToken ct = default)
    {
        var existing = await _context.DailySummaries
            .FirstOrDefaultAsync(d => d.AccountId == summary.AccountId && d.Date == summary.Date, ct);

        if (existing is null)
            await _context.DailySummaries.AddAsync(summary, ct);
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(summary);
            existing.ClearDomainEvents();
        }

        await _context.SaveChangesAsync(ct);
    }
}

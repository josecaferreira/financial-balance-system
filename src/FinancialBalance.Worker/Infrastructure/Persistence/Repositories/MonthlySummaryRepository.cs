using FinancialBalance.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FinancialBalance.Worker.Infrastructure.Persistence.Repositories;

public class MonthlySummaryRepository : IMonthlySummaryRepository
{
    private readonly WorkerDbContext _context;

    public MonthlySummaryRepository(WorkerDbContext context)
        => _context = context;

    public async Task<MonthlySummary?> GetAsync(Guid accountId, int year, int month, CancellationToken ct = default)
        => await _context.MonthlySummaries
            .Include("CategoryBreakdowns")
            .FirstOrDefaultAsync(m => m.AccountId == accountId && m.Year == year && m.Month == month, ct);

    public async Task<IReadOnlyList<MonthlySummary>> GetRangeAsync(
        Guid accountId, int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken ct = default)
        => await _context.MonthlySummaries
            .Include("CategoryBreakdowns")
            .Where(m => m.AccountId == accountId
                && (m.Year * 12 + m.Month) >= (fromYear * 12 + fromMonth)
                && (m.Year * 12 + m.Month) <= (toYear * 12 + toMonth))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync(ct);

    public async Task UpsertAsync(MonthlySummary summary, CancellationToken ct = default)
    {
        var existing = await _context.MonthlySummaries
            .FirstOrDefaultAsync(m => m.AccountId == summary.AccountId
                && m.Year == summary.Year && m.Month == summary.Month, ct);

        if (existing is null)
            await _context.MonthlySummaries.AddAsync(summary, ct);
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(summary);
            existing.ClearDomainEvents();
        }

        await _context.SaveChangesAsync(ct);
    }
}

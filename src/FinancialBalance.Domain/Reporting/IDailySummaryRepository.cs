namespace FinancialBalance.Domain.Reporting;

public interface IDailySummaryRepository
{
    Task<DailySummary?> GetAsync(Guid accountId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailySummary>> GetRangeAsync(Guid accountId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task UpsertAsync(DailySummary summary, CancellationToken ct = default);
}

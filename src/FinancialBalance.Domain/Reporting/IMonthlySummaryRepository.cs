namespace FinancialBalance.Domain.Reporting;

public interface IMonthlySummaryRepository
{
    Task<MonthlySummary?> GetAsync(Guid accountId, int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlySummary>> GetRangeAsync(Guid accountId, int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken ct = default);
    Task UpsertAsync(MonthlySummary summary, CancellationToken ct = default);
}

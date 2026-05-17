using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Reporting;

public class MonthlySummary : AggregateRoot
{
    public Guid AccountId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public decimal TotalIncoming { get; private set; }
    public decimal TotalOutgoing { get; private set; }
    public decimal NetBalance { get; private set; }
    public int TransactionCount { get; private set; }
    public DateTime ComputedAt { get; private set; }

    private readonly List<CategoryBreakdown> _categoryBreakdowns = new();
    public IReadOnlyCollection<CategoryBreakdown> CategoryBreakdowns => _categoryBreakdowns.AsReadOnly();

    private MonthlySummary() { }

    public static MonthlySummary ComputeFrom(
        Guid accountId,
        int year,
        int month,
        decimal openingBalance,
        IEnumerable<DailySummary> dailySummaries)
    {
        var summaries = dailySummaries.ToList();

        var totalIncoming = summaries.Sum(d => d.TotalIncoming);
        var totalOutgoing = summaries.Sum(d => d.TotalOutgoing);
        var net = totalIncoming - totalOutgoing;

        var categoryMap = new Dictionary<string, CategoryBreakdown>();
        foreach (var day in summaries)
        {
            foreach (var cb in day.CategoryBreakdowns)
            {
                if (!categoryMap.TryGetValue(cb.Category, out var agg))
                {
                    agg = new CategoryBreakdown { Category = cb.Category };
                    categoryMap[cb.Category] = agg;
                }
                agg.TotalIncoming += cb.TotalIncoming;
                agg.TotalOutgoing += cb.TotalOutgoing;
            }
        }

        var monthly = new MonthlySummary
        {
            AccountId = accountId,
            Year = year,
            Month = month,
            OpeningBalance = openingBalance,
            ClosingBalance = openingBalance + net,
            TotalIncoming = totalIncoming,
            TotalOutgoing = totalOutgoing,
            NetBalance = net,
            TransactionCount = summaries.Sum(d => d.TransactionCount),
            ComputedAt = DateTime.UtcNow
        };

        monthly._categoryBreakdowns.AddRange(categoryMap.Values);
        return monthly;
    }
}

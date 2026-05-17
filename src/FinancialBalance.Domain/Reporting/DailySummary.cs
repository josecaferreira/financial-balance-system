using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Reporting;

public class DailySummary : AggregateRoot
{
    public Guid AccountId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal TotalIncoming { get; private set; }
    public decimal TotalOutgoing { get; private set; }
    public decimal NetBalance { get; private set; }
    public int TransactionCount { get; private set; }
    public DateTime ComputedAt { get; private set; }

    private readonly List<CategoryBreakdown> _categoryBreakdowns = new();
    public IReadOnlyCollection<CategoryBreakdown> CategoryBreakdowns => _categoryBreakdowns.AsReadOnly();

    private DailySummary() { }

    public static DailySummary Create(Guid accountId, DateOnly date)
        => new()
        {
            AccountId = accountId,
            Date = date,
            TotalIncoming = 0m,
            TotalOutgoing = 0m,
            NetBalance = 0m,
            TransactionCount = 0,
            ComputedAt = DateTime.UtcNow
        };

    public void ApplyTransaction(string transactionType, decimal amount, string category)
    {
        var isIncoming = transactionType.Equals("Incoming", StringComparison.OrdinalIgnoreCase);

        if (isIncoming)
            TotalIncoming += amount;
        else
            TotalOutgoing += amount;

        NetBalance = TotalIncoming - TotalOutgoing;
        TransactionCount++;
        ComputedAt = DateTime.UtcNow;
        SetUpdated();

        var breakdown = _categoryBreakdowns.FirstOrDefault(b => b.Category == category);
        if (breakdown is null)
        {
            breakdown = new CategoryBreakdown { Category = category };
            _categoryBreakdowns.Add(breakdown);
        }

        if (isIncoming) breakdown.TotalIncoming += amount;
        else breakdown.TotalOutgoing += amount;
    }

    public void ReverseTransaction(string transactionType, decimal amount, string category)
    {
        var isIncoming = transactionType.Equals("Incoming", StringComparison.OrdinalIgnoreCase);

        if (isIncoming)
            TotalIncoming -= amount;
        else
            TotalOutgoing -= amount;

        NetBalance = TotalIncoming - TotalOutgoing;
        TransactionCount = Math.Max(0, TransactionCount - 1);
        ComputedAt = DateTime.UtcNow;
        SetUpdated();

        var breakdown = _categoryBreakdowns.FirstOrDefault(b => b.Category == category);
        if (breakdown is not null)
        {
            if (isIncoming) breakdown.TotalIncoming -= amount;
            else breakdown.TotalOutgoing -= amount;
        }
    }
}

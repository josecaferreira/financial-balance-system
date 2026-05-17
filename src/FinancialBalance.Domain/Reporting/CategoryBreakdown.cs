namespace FinancialBalance.Domain.Reporting;

public class CategoryBreakdown
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = default!;
    public decimal TotalIncoming { get; set; }
    public decimal TotalOutgoing { get; set; }
}

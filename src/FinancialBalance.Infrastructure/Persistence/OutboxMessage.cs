namespace FinancialBalance.Infrastructure.Persistence;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
}

using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Accounts.Events;

public record TransactionCreated(
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    DateTime TransactionDate) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

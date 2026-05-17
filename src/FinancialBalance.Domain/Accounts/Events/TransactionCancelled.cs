using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Accounts.Events;

public record TransactionCancelled(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    TransactionType OriginalType) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

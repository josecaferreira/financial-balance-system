using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Accounts.Events;

public record AccountBalanceUpdated(
    Guid AccountId,
    decimal NewBalance,
    decimal PreviousBalance,
    DateTime UpdatedAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

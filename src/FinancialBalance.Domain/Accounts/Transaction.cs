using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Accounts;

public class Transaction : Entity
{
    public Guid AccountId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public TransactionCategory Category { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public Guid CreatedBy { get; private set; }

    private Transaction() { }

    internal static Transaction Create(
        Guid accountId,
        TransactionType type,
        decimal amount,
        string description,
        TransactionCategory category,
        DateOnly transactionDate,
        Guid createdBy,
        string? referenceNumber = null)
    {
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.");

        if (transactionDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("Transaction date cannot be in the future.");

        return new Transaction
        {
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Description = description,
            Category = category,
            ReferenceNumber = referenceNumber,
            Status = TransactionStatus.Confirmed,
            TransactionDate = transactionDate,
            CreatedBy = createdBy
        };
    }

    internal void Cancel()
    {
        if (Status == TransactionStatus.Cancelled)
            throw new DomainException("Transaction is already cancelled.");

        Status = TransactionStatus.Cancelled;
        SetUpdated();
    }
}

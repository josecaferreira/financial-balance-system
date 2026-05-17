using FinancialBalance.Domain.Accounts.Events;
using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Accounts;

public class Account : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public AccountType Type { get; private set; }
    public Currency Currency { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    private Account() { }

    public static Account Create(string name, string code, AccountType type, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Account name is required.");

        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Account code is required.");

        return new Account
        {
            Name = name,
            Code = code.ToUpperInvariant(),
            Type = type,
            Currency = currency,
            CurrentBalance = 0m,
            IsActive = true
        };
    }

    public Transaction RegisterTransaction(
        TransactionType type,
        decimal amount,
        string description,
        TransactionCategory category,
        DateOnly transactionDate,
        Guid createdBy,
        string? referenceNumber = null)
    {
        if (!IsActive)
            throw new DomainException("Cannot register a transaction on an inactive account.");

        var transaction = Transaction.Create(
            Id, type, amount, description, category, transactionDate, createdBy, referenceNumber);

        _transactions.Add(transaction);

        var previousBalance = CurrentBalance;
        CurrentBalance = type == TransactionType.Incoming
            ? CurrentBalance + amount
            : CurrentBalance - amount;

        SetUpdated();

        RaiseDomainEvent(new TransactionCreated(
            transaction.Id, Id, type, amount, transactionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));

        RaiseDomainEvent(new AccountBalanceUpdated(
            Id, CurrentBalance, previousBalance, DateTime.UtcNow));

        return transaction;
    }

    public void CancelTransaction(Guid transactionId)
    {
        var transaction = _transactions.FirstOrDefault(t => t.Id == transactionId)
            ?? throw new DomainException($"Transaction {transactionId} not found in account.");

        var previousBalance = CurrentBalance;
        transaction.Cancel();

        CurrentBalance = transaction.Type == TransactionType.Incoming
            ? CurrentBalance - transaction.Amount
            : CurrentBalance + transaction.Amount;

        SetUpdated();

        RaiseDomainEvent(new TransactionCancelled(
            transaction.Id, Id, transaction.Amount, transaction.Type));

        RaiseDomainEvent(new AccountBalanceUpdated(
            Id, CurrentBalance, previousBalance, DateTime.UtcNow));
    }

    public void Deactivate()
    {
        if (_transactions.Any(t => t.Status == TransactionStatus.Pending))
            throw new DomainException("Cannot deactivate an account with pending transactions.");

        IsActive = false;
        SetUpdated();
    }
}

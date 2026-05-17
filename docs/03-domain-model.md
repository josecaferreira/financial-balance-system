# 03 — Domain Model

## Bounded Contexts

```
┌──────────────────────────────┐   ┌──────────────────────────────┐
│     Transaction Context       │   │      Reporting Context        │
│                               │   │                               │
│  Account                      │   │  DailySummary                 │
│  Transaction                  │   │  MonthlySummary               │
│  Balance                      │   │  ReportRequest                │
│  TransactionCategory          │   │                               │
└──────────────────────────────┘   └──────────────────────────────┘
```

---

## Entities & Aggregates

### Account (Aggregate Root)
```csharp
public class Account : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }          // e.g. "MAIN-001"
    public AccountType Type { get; private set; }     // Checking, Savings, CostCenter
    public Currency Currency { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public void RegisterTransaction(Transaction transaction)
    {
        // domain logic: update CurrentBalance, raise domain event
    }
}
```

### Transaction (Entity)
```csharp
public class Transaction : Entity
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public TransactionType Type { get; private set; }  // Incoming, Outgoing
    public decimal Amount { get; private set; }
    public string Description { get; private set; }
    public TransactionCategory Category { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
}
```

### Balance (Value Object)
```csharp
public record Balance(
    decimal TotalIncoming,
    decimal TotalOutgoing,
    decimal Net,
    DateOnly Date,
    Guid AccountId
)
{
    public static Balance Calculate(IEnumerable<Transaction> transactions, DateOnly date, Guid accountId)
    {
        var incoming = transactions.Where(t => t.Type == TransactionType.Incoming).Sum(t => t.Amount);
        var outgoing = transactions.Where(t => t.Type == TransactionType.Outgoing).Sum(t => t.Amount);
        return new Balance(incoming, outgoing, incoming - outgoing, date, accountId);
    }
}
```

### DailySummary (Aggregate Root — Reporting Context)
```csharp
public class DailySummary : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal TotalIncoming { get; private set; }
    public decimal TotalOutgoing { get; private set; }
    public decimal NetBalance { get; private set; }
    public int TransactionCount { get; private set; }
    public DateTime ComputedAt { get; private set; }
}
```

### MonthlySummary (Aggregate Root — Reporting Context)
```csharp
public class MonthlySummary : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal TotalIncoming { get; private set; }
    public decimal TotalOutgoing { get; private set; }
    public decimal NetBalance { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public IReadOnlyList<DailySummary> DailyBreakdown { get; private set; }
    public DateTime ComputedAt { get; private set; }
}
```

---

## Enumerations

```csharp
public enum TransactionType    { Incoming, Outgoing }
public enum TransactionStatus  { Pending, Confirmed, Cancelled, Failed }
public enum AccountType        { Checking, Savings, CostCenter, CreditCard }
public enum Currency           { BRL, USD, EUR }

public enum TransactionCategory
{
    // Incoming
    Revenue, Investment, Loan, Refund, Other,
    // Outgoing
    Payroll, Supplier, Tax, Utility, Rent, Marketing, IT, Travel
}
```

---

## Domain Events

```csharp
public record TransactionCreated(
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    DateTime TransactionDate
) : IDomainEvent;

public record TransactionCancelled(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    TransactionType OriginalType
) : IDomainEvent;

public record AccountBalanceUpdated(
    Guid AccountId,
    decimal NewBalance,
    decimal PreviousBalance,
    DateTime UpdatedAt
) : IDomainEvent;
```

---

## Invariants

| Entity | Invariant |
|---|---|
| Transaction | Amount must be > 0 |
| Transaction | TransactionDate cannot be in the future |
| Transaction | Cancelled transactions cannot be modified |
| Account | CurrentBalance reflects sum of all Confirmed transactions |
| Account | Cannot deactivate account with pending transactions |
| MonthlySummary | ClosingBalance = OpeningBalance + NetBalance |

---

## Project Structure

```
src/
├── FinancialBalance.Domain/
│   ├── Accounts/
│   │   ├── Account.cs
│   │   ├── Transaction.cs
│   │   ├── Balance.cs
│   │   └── Events/
│   ├── Reporting/
│   │   ├── DailySummary.cs
│   │   └── MonthlySummary.cs
│   └── Shared/
│       ├── AggregateRoot.cs
│       ├── Entity.cs
│       └── IDomainEvent.cs
├── FinancialBalance.Application/
│   ├── Transactions/
│   │   ├── Commands/
│   │   └── Queries/
│   └── Reports/
│       └── Queries/
├── FinancialBalance.Infrastructure/
│   ├── Persistence/
│   ├── Messaging/
│   └── Cache/
├── FinancialBalance.Api/          ← Transaction API
├── FinancialBalance.ReportingApi/ ← Reporting API
└── FinancialBalance.Worker/       ← Reporting Worker
```

# 05 — Database Schema

## Overview

PostgreSQL 16. All monetary values use `NUMERIC(18,2)` for financial precision. UUIDs as primary keys. Timestamps stored as `TIMESTAMPTZ` (UTC).

---

## Schema: `finance` (Transaction Context)

### `accounts`
```sql
CREATE TABLE finance.accounts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(200) NOT NULL,
    code            VARCHAR(50)  NOT NULL UNIQUE,
    type            VARCHAR(50)  NOT NULL,   -- Checking, Savings, CostCenter, CreditCard
    currency        CHAR(3)      NOT NULL DEFAULT 'BRL',
    current_balance NUMERIC(18,2) NOT NULL DEFAULT 0.00,
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_accounts_code     ON finance.accounts (code);
CREATE INDEX idx_accounts_is_active ON finance.accounts (is_active);
```

### `transactions`
```sql
CREATE TABLE finance.transactions (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id       UUID         NOT NULL REFERENCES finance.accounts(id),
    type             VARCHAR(20)  NOT NULL,  -- Incoming, Outgoing
    amount           NUMERIC(18,2) NOT NULL CHECK (amount > 0),
    description      VARCHAR(500) NOT NULL,
    category         VARCHAR(100) NOT NULL,
    reference_number VARCHAR(100),
    status           VARCHAR(20)  NOT NULL DEFAULT 'Confirmed',
    transaction_date DATE         NOT NULL,
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by       UUID         NOT NULL
) PARTITION BY RANGE (transaction_date);

-- Monthly partitions (auto-created by migration job)
CREATE TABLE finance.transactions_2024_01
    PARTITION OF finance.transactions
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

CREATE TABLE finance.transactions_2024_02
    PARTITION OF finance.transactions
    FOR VALUES FROM ('2024-02-01') TO ('2024-03-01');
-- ... etc

CREATE INDEX idx_transactions_account_date
    ON finance.transactions (account_id, transaction_date DESC);

CREATE INDEX idx_transactions_type
    ON finance.transactions (type, transaction_date DESC);

CREATE INDEX idx_transactions_category
    ON finance.transactions (category, transaction_date DESC);

CREATE INDEX idx_transactions_reference
    ON finance.transactions (reference_number)
    WHERE reference_number IS NOT NULL;

CREATE UNIQUE INDEX idx_transactions_reference_unique
    ON finance.transactions (account_id, reference_number)
    WHERE reference_number IS NOT NULL AND status != 'Cancelled';
```

### `outbox_messages` (Transactional Outbox Pattern)
```sql
CREATE TABLE finance.outbox_messages (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    type         VARCHAR(200) NOT NULL,
    payload      JSONB        NOT NULL
);

CREATE INDEX idx_outbox_unprocessed
    ON finance.outbox_messages (created_at)
    WHERE processed_at IS NULL;
```

---

## Schema: `reporting` (Reporting Context)

### `daily_summaries`
```sql
CREATE TABLE reporting.daily_summaries (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id        UUID        NOT NULL,
    date              DATE        NOT NULL,
    total_incoming    NUMERIC(18,2) NOT NULL DEFAULT 0.00,
    total_outgoing    NUMERIC(18,2) NOT NULL DEFAULT 0.00,
    net_balance       NUMERIC(18,2) NOT NULL,
    transaction_count INTEGER     NOT NULL DEFAULT 0,
    computed_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (account_id, date)
);

CREATE INDEX idx_daily_summaries_account_date
    ON reporting.daily_summaries (account_id, date DESC);
```

### `monthly_summaries`
```sql
CREATE TABLE reporting.monthly_summaries (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id        UUID        NOT NULL,
    year              SMALLINT    NOT NULL,
    month             SMALLINT    NOT NULL CHECK (month BETWEEN 1 AND 12),
    opening_balance   NUMERIC(18,2) NOT NULL,
    closing_balance   NUMERIC(18,2) NOT NULL,
    total_incoming    NUMERIC(18,2) NOT NULL DEFAULT 0.00,
    total_outgoing    NUMERIC(18,2) NOT NULL DEFAULT 0.00,
    net_balance       NUMERIC(18,2) NOT NULL,
    transaction_count INTEGER     NOT NULL DEFAULT 0,
    computed_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (account_id, year, month)
);

CREATE INDEX idx_monthly_summaries_account
    ON reporting.monthly_summaries (account_id, year DESC, month DESC);
```

### `category_breakdowns`
```sql
CREATE TABLE reporting.category_breakdowns (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    summary_type   VARCHAR(10)   NOT NULL,  -- 'daily' or 'monthly'
    summary_id     UUID          NOT NULL,
    category       VARCHAR(100)  NOT NULL,
    total_incoming NUMERIC(18,2) NOT NULL DEFAULT 0.00,
    total_outgoing NUMERIC(18,2) NOT NULL DEFAULT 0.00
);

CREATE INDEX idx_category_breakdowns_summary
    ON reporting.category_breakdowns (summary_type, summary_id);
```

---

## EF Core Configuration Example

```csharp
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", "finance");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .HasColumnType("date");

        builder.HasOne<Account>()
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId);
    }
}
```

---

## Migration Strategy

- EF Core Migrations for schema changes
- Partitions created automatically by a monthly maintenance job (Kubernetes CronJob)
- Read replicas via PostgreSQL streaming replication — no additional setup required
- Indexes created `CONCURRENTLY` in production to avoid table locks

---

## Data Retention

| Table | Retention | Strategy |
|---|---|---|
| `transactions` | 7 years (legal) | Partition detach + archive to cold storage |
| `daily_summaries` | 5 years | DELETE by date range |
| `monthly_summaries` | 10 years | Keep indefinitely (small table) |
| `outbox_messages` | 30 days | DELETE processed messages older than 30 days |

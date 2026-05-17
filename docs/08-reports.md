# 08 — Report Generation

## Overview

Reports are generated asynchronously by the Reporting Worker, which consumes `TransactionCreated` and `TransactionCancelled` events from RabbitMQ. Pre-computed aggregates are stored in PostgreSQL and cached in Redis.

---

## Daily Report Pipeline

```
TransactionCreated event
        │
        ▼
Reporting Worker receives event
        │
        ▼
Upsert DailySummary for (accountId, date)
  - Recalculate total_incoming, total_outgoing, net_balance
  - Update transaction_count
  - Update category_breakdowns
        │
        ▼
Invalidate Redis cache key: daily:{accountId}:{date}
        │
        ▼
Next GET /reports/daily → reads from DB, caches in Redis (TTL: 1h)
```

### Worker Consumer

```csharp
public class TransactionCreatedConsumer : IConsumer<TransactionCreated>
{
    public async Task Consume(ConsumeContext<TransactionCreated> context)
    {
        var msg = context.Message;

        await _dailySummaryService.UpsertAsync(
            msg.AccountId,
            DateOnly.FromDateTime(msg.TransactionDate),
            msg.Type,
            msg.Amount,
            msg.Category
        );

        await _cache.RemoveAsync($"daily:{msg.AccountId}:{msg.TransactionDate:yyyy-MM-dd}");
    }
}
```

### Upsert Logic (PostgreSQL)

```sql
INSERT INTO reporting.daily_summaries
    (id, account_id, date, total_incoming, total_outgoing, net_balance, transaction_count, computed_at)
VALUES
    (gen_random_uuid(), @accountId, @date,
     CASE WHEN @type = 'Incoming' THEN @amount ELSE 0 END,
     CASE WHEN @type = 'Outgoing' THEN @amount ELSE 0 END,
     CASE WHEN @type = 'Incoming' THEN @amount ELSE -@amount END,
     1, NOW())
ON CONFLICT (account_id, date) DO UPDATE SET
    total_incoming    = daily_summaries.total_incoming
                      + CASE WHEN @type = 'Incoming' THEN @amount ELSE 0 END,
    total_outgoing    = daily_summaries.total_outgoing
                      + CASE WHEN @type = 'Outgoing' THEN @amount ELSE 0 END,
    net_balance       = daily_summaries.net_balance
                      + CASE WHEN @type = 'Incoming' THEN @amount ELSE -@amount END,
    transaction_count = daily_summaries.transaction_count + 1,
    computed_at       = NOW();
```

---

## Monthly Report Pipeline

Monthly summaries are derived from daily summaries to keep computation cheap.

### Trigger: End-of-Day Job

A Kubernetes CronJob runs at `00:05` daily and rolls up daily summaries into the monthly aggregate:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: monthly-rollup
spec:
  schedule: "5 0 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: rollup
              image: financialbalance/reporting-worker:latest
              args: ["--job=monthly-rollup", "--date=yesterday"]
```

### Rollup Query

```sql
INSERT INTO reporting.monthly_summaries
    (id, account_id, year, month, opening_balance, closing_balance,
     total_incoming, total_outgoing, net_balance, transaction_count, computed_at)
SELECT
    gen_random_uuid(),
    account_id,
    EXTRACT(YEAR FROM date)::SMALLINT,
    EXTRACT(MONTH FROM date)::SMALLINT,
    0,  -- opening_balance calculated separately
    0,  -- closing_balance calculated separately
    SUM(total_incoming),
    SUM(total_outgoing),
    SUM(net_balance),
    SUM(transaction_count),
    NOW()
FROM reporting.daily_summaries
WHERE date >= DATE_TRUNC('month', @targetDate)
  AND date <  DATE_TRUNC('month', @targetDate) + INTERVAL '1 month'
GROUP BY account_id, EXTRACT(YEAR FROM date), EXTRACT(MONTH FROM date)
ON CONFLICT (account_id, year, month) DO UPDATE SET
    total_incoming    = EXCLUDED.total_incoming,
    total_outgoing    = EXCLUDED.total_outgoing,
    net_balance       = EXCLUDED.net_balance,
    transaction_count = EXCLUDED.transaction_count,
    computed_at       = NOW();
```

---

## Caching Strategy

| Cache Key | TTL | Invalidated When |
|---|---|---|
| `daily:{accountId}:{date}` | 1 hour (past dates: 24h) | New transaction on that date |
| `monthly:{accountId}:{year}:{month}` | 6 hours (past months: 24h) | Daily rollup runs |
| `account:{accountId}:balance` | 5 minutes | Any new transaction |

```csharp
public async Task<DailyReportDto> GetDailyReportAsync(Guid accountId, DateOnly date)
{
    var cacheKey = $"daily:{accountId}:{date:yyyy-MM-dd}";
    var cached = await _cache.GetAsync<DailyReportDto>(cacheKey);
    if (cached is not null) return cached;

    var report = await _repository.GetDailySummaryAsync(accountId, date);
    var ttl = date < DateOnly.FromDateTime(DateTime.UtcNow) ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1);

    await _cache.SetAsync(cacheKey, report, ttl);
    return report;
}
```

---

## Report Export (PDF / CSV)

Reports can be exported asynchronously:

```
POST /reports/export
{
  "accountId": "...",
  "type": "Monthly",
  "year": 2024,
  "month": 1,
  "format": "PDF"
}

→ 202 Accepted
{
  "exportId": "abc123",
  "statusUrl": "/reports/export/abc123/status"
}
```

Export generation runs in the Reporting Worker. Completed files are stored in Blob Storage (S3/Azure Blob) and a signed URL is returned via `GET /reports/export/{id}`.

---

## Scheduled Reports

Monthly reports are automatically emailed on the 1st of each month via a CronJob:

```yaml
schedule: "0 8 1 * *"  # 08:00 on the 1st of every month
```

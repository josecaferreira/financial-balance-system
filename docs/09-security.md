# 09 — Security

## Authentication

### JWT Bearer Tokens

All API endpoints require a valid JWT token issued by the Identity Provider (IdP).

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience  = builder.Configuration["Auth:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ClockSkew                = TimeSpan.FromSeconds(30)
        };
    });
```

### Supported Flows

| Flow | Use Case |
|---|---|
| Authorization Code + PKCE | Web/SPA clients (Finance team dashboard) |
| Client Credentials | Service-to-service (ERP integration) |
| Refresh Token | Long-lived sessions for dashboard users |

---

## Authorization

Role-based access control (RBAC) enforced via JWT claims.

| Role | Permissions |
|---|---|
| `finance.admin` | Full CRUD on accounts and transactions, access all reports |
| `finance.operator` | Create/read transactions, read reports |
| `finance.viewer` | Read-only access to reports |
| `service.erp` | Create transactions only (machine account) |

```csharp
// Policy definitions
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanWriteTransactions", p =>
        p.RequireRole("finance.admin", "finance.operator", "service.erp"))
    .AddPolicy("CanViewReports", p =>
        p.RequireRole("finance.admin", "finance.operator", "finance.viewer"))
    .AddPolicy("CanManageAccounts", p =>
        p.RequireRole("finance.admin"));

// Usage in controller
[Authorize(Policy = "CanWriteTransactions")]
[HttpPost("transactions")]
public async Task<IActionResult> CreateTransaction(...)
```

---

## Input Validation

All incoming requests are validated via FluentValidation before reaching the domain layer.

```csharp
public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999.99m)
            .WithMessage("Amount must be between 0.01 and 999,999,999.99");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500)
            .Matches(@"^[\w\s\-\.,#\/\(\)]+$")
            .WithMessage("Description contains invalid characters");

        RuleFor(x => x.TransactionDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Transaction date cannot be in the future");

        RuleFor(x => x.AccountId)
            .NotEmpty();
    }
}
```

---

## Data Protection

### Financial Data at Rest
- PostgreSQL data encrypted at rest using cloud provider disk encryption (AES-256)
- Database credentials stored in Kubernetes Secrets (or HashiCorp Vault in production)
- Connection strings never stored in code or docker images — injected via environment variables

### Financial Data in Transit
- TLS 1.2+ enforced on all ingress routes via cert-manager
- Internal service-to-service communication uses cluster-internal DNS (not exposed externally)
- RabbitMQ connections use TLS in production

### Secrets Management
```yaml
# Kubernetes Secret (base64 encoded values)
apiVersion: v1
kind: Secret
metadata:
  name: financial-balance-secrets
type: Opaque
data:
  postgres-connection-string: <base64>
  redis-connection-string: <base64>
  rabbitmq-password: <base64>
  jwt-secret: <base64>
```

For production, secrets are managed by **AWS Secrets Manager** or **HashiCorp Vault** and injected via the External Secrets Operator.

---

## Audit Logging

Every data-modifying operation is audit-logged.

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct)
    {
        var entries = eventData.Context!.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (var entry in entries)
        {
            _auditLog.Add(new AuditEntry
            {
                EntityType  = entry.Entity.GetType().Name,
                EntityId    = entry.Property("Id").CurrentValue?.ToString(),
                Action      = entry.State.ToString(),
                UserId      = _currentUser.Id,
                Timestamp   = DateTime.UtcNow,
                Changes     = SerializeChanges(entry)
            });
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

Audit logs are written to a dedicated `audit.audit_log` table and shipped to the ELK stack for SIEM analysis.

---

## Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit         = 100;
        limiter.Window              = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit          = 10;
    });

    options.AddFixedWindowLimiter("reports", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window      = TimeSpan.FromMinutes(1);
    });
});
```

---

## Security Checklist

| Control | Status |
|---|---|
| JWT authentication on all endpoints | Required |
| RBAC with least-privilege roles | Required |
| TLS 1.2+ on all ingress | Required |
| Input validation (FluentValidation) | Required |
| SQL injection prevention (EF Core parameterized) | Built-in |
| Audit logging for all mutations | Required |
| Secrets via Kubernetes Secrets / Vault | Required |
| Container runs as non-root user | Required |
| Rate limiting per role/endpoint | Required |
| CORS policy (explicit allowlist) | Required |
| Security headers (HSTS, CSP, X-Frame-Options) | Required |
| Container image scanning (Trivy in CI) | Required |
| Dependency vulnerability scanning | Required |

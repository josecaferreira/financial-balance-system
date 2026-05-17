# 02 — Technology Stack

## Runtime & Language

| Component | Choice | Justification |
|---|---|---|
| Runtime | .NET 8 (LTS) | Long-term support until Nov 2026, best performance in .NET history |
| Language | C# 12 | Primary patterns: records, primary constructors, collection expressions |
| Hosting | ASP.NET Core 8 | Minimal APIs + Controllers, built-in DI, Kestrel |

---

## Backend Libraries

### Core
| Library | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 8.x | ORM for write path |
| `Dapper` | 2.x | Micro-ORM for complex report queries |
| `MassTransit` | 8.x | RabbitMQ abstraction, message routing, retry policies |
| `MediatR` | 12.x | CQRS mediator for commands/queries/domain events |
| `FluentValidation` | 11.x | Input validation for API requests |
| `AutoMapper` | 13.x | DTO ↔ domain mapping |

### Resilience
| Library | Version | Purpose |
|---|---|---|
| `Polly` | 8.x | Retry, circuit breaker, timeout policies |
| `Microsoft.Extensions.Http.Resilience` | 8.x | HTTP client resilience pipelines |

### Auth & Security
| Library | Version | Purpose |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.x | JWT token validation |
| `BCrypt.Net-Next` | 4.x | Password hashing |

### Observability
| Library | Version | Purpose |
|---|---|---|
| `OpenTelemetry.Extensions.Hosting` | 1.x | Tracing + metrics instrumentation |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | 1.x | Prometheus metrics endpoint |
| `Serilog.AspNetCore` | 8.x | Structured logging |
| `Serilog.Sinks.Elasticsearch` | 10.x | Log shipping to ELK stack |

---

## Data Layer

| Technology | Version | Role |
|---|---|---|
| PostgreSQL | 16 | Primary relational database |
| Redis | 7 | Report caching, distributed locking |
| RabbitMQ | 3.13 | Async messaging between services |

**PostgreSQL** was chosen over SQL Server for:
- Open-source, no licensing cost
- Excellent JSON support for flexible report payloads
- Time-based table partitioning (critical for daily/monthly aggregates)
- Native support for `NUMERIC` type for financial precision

---

## Infrastructure

| Technology | Role |
|---|---|
| Docker | Container runtime, local dev with docker-compose |
| Kubernetes | Production orchestration |
| Helm | Kubernetes package manager |
| KEDA | Event-driven autoscaling for workers |
| NGINX Ingress | API gateway / reverse proxy |
| cert-manager | Automatic TLS certificate management |

---

## CI/CD & Tooling

| Tool | Purpose |
|---|---|
| GitHub Actions | CI/CD pipelines |
| Docker Hub / ECR | Container registry |
| SonarQube | Static code analysis |
| Trivy | Container image vulnerability scanning |
| Prometheus + Grafana | Metrics and dashboards |
| Jaeger | Distributed tracing UI |

---

## Development Tools

| Tool | Purpose |
|---|---|
| `dotnet-ef` CLI | EF Core migrations |
| `Bogus` | Fake data generation for tests |
| `xUnit` | Unit and integration testing |
| `FluentAssertions` | Expressive test assertions |
| `Testcontainers` | Spin up real PostgreSQL/Redis/RabbitMQ in tests |
| Swagger / Scalar | API documentation UI |

---

## Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        .NET 8 Application                        │
│                                                                   │
│  ASP.NET Core 8                                                   │
│    ├── MediatR (CQRS dispatch)                                    │
│    ├── FluentValidation (input validation)                        │
│    ├── JWT Bearer Auth                                            │
│    └── OpenTelemetry (traces + metrics)                           │
│                                                                   │
│  Data Access                                                      │
│    ├── EF Core 8 → PostgreSQL (writes)                            │
│    ├── Dapper → PostgreSQL read replica (reports)                 │
│    └── StackExchange.Redis → Redis (cache)                        │
│                                                                   │
│  Messaging                                                        │
│    └── MassTransit → RabbitMQ                                     │
│                                                                   │
│  Cross-cutting                                                    │
│    ├── Serilog (structured logs)                                  │
│    └── Polly (resilience)                                         │
└─────────────────────────────────────────────────────────────────┘
```

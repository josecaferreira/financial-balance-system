# 01 — System Architecture

## Overview

The Financial Balance Management System is a cloud-native, microservices-based application built on .NET C# that handles all incoming and outgoing financial transactions for a company, with daily and monthly reporting capabilities.

---

## High-Level Architecture

```
                        ┌─────────────────────────────────────────────┐
                        │              API Gateway / Ingress            │
                        │          (NGINX Ingress / AWS ALB)            │
                        └──────────────┬──────────────┬────────────────┘
                                       │              │
                        ┌──────────────▼──┐    ┌──────▼──────────────┐
                        │  Transaction API │    │   Reporting API      │
                        │  (.NET C# 8)     │    │   (.NET C# 8)        │
                        │  HPA: 2–20 pods  │    │   HPA: 2–10 pods     │
                        └──────────────┬──┘    └──────┬──────────────┘
                                       │              │
                        ┌──────────────▼──────────────▼────────────────┐
                        │                  Message Bus                   │
                        │              (RabbitMQ / Azure SB)             │
                        └────────────────────┬─────────────────────────┘
                                             │
                        ┌────────────────────▼─────────────────────────┐
                        │             Reporting Worker                   │
                        │          (.NET Background Service)             │
                        │        KEDA: 1–10 pods (queue-depth)          │
                        └────────────────────┬─────────────────────────┘
                                             │
               ┌─────────────────────────────┼──────────────────────────┐
               │                             │                           │
   ┌───────────▼──────────┐    ┌─────────────▼──────────┐  ┌───────────▼──────────┐
   │  PostgreSQL Primary  │    │      Redis Cache        │  │   Blob Storage       │
   │  + Read Replicas     │    │  (Report caching)       │  │  (Report exports)    │
   └──────────────────────┘    └────────────────────────┘  └──────────────────────┘
```

---

## Services

### 1. Transaction API
- **Responsibility**: Accepts and processes all financial transactions (incoming/outgoing)
- **Technology**: ASP.NET Core 8 Web API
- **Scaling**: Kubernetes HPA, CPU target 70%, 2–20 replicas
- **Persistence**: PostgreSQL via EF Core (write path)
- **Events**: Publishes `TransactionCreated`, `TransactionUpdated` to RabbitMQ

### 2. Reporting API
- **Responsibility**: Serves daily and monthly financial reports
- **Technology**: ASP.NET Core 8 Web API
- **Scaling**: Kubernetes HPA, CPU target 60%, 2–10 replicas
- **Persistence**: PostgreSQL read replicas + Redis cache
- **Pattern**: CQRS read side — queries only, no writes

### 3. Reporting Worker
- **Responsibility**: Consumes events and pre-computes report aggregates
- **Technology**: .NET 8 Background Service (IHostedService)
- **Scaling**: KEDA ScaledObject driven by RabbitMQ queue depth
- **Persistence**: Writes aggregated report data back to PostgreSQL

---

## Architecture Patterns

| Pattern | Applied To | Reason |
|---|---|---|
| CQRS | Transaction write / Reporting read | Separate scaling, different consistency needs |
| Event-Driven | Transaction → Worker | Decouples processing, enables async aggregation |
| Repository | All data access | Testability, abstraction over EF Core |
| Outbox Pattern | Transaction API | Guarantees event delivery without distributed transactions |
| Domain Events | Domain layer | Encapsulates business logic side effects |

---

## C4 Context Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│                        System Context                               │
│                                                                     │
│   [Finance Team] ──────► [Financial Balance System] ◄─── [ERP]    │
│                                    │                                │
│                          [External Bank APIs]                       │
└────────────────────────────────────────────────────────────────────┘
```

### External Actors
- **Finance Team**: Registers transactions, generates and views reports
- **ERP System**: Pushes transaction data via API integration
- **External Bank APIs**: Optional reconciliation data source

---

## Data Flow

### Transaction Write Path
```
Client → API Gateway → Transaction API → PostgreSQL (write)
                                       → RabbitMQ (TransactionCreated event)
                                            └→ Reporting Worker → PostgreSQL (aggregates)
```

### Report Read Path
```
Client → API Gateway → Reporting API → Redis Cache (hit?)
                                     → PostgreSQL Read Replica (miss)
                                     → Redis Cache (store)
                                     → Client
```

---

## Deployment Model

- **Container Runtime**: Docker
- **Orchestration**: Kubernetes (EKS / GKE / AKS)
- **Auto-scaling**:
  - HPA for API pods (CPU/memory metrics)
  - KEDA for worker pods (RabbitMQ queue depth)
  - Cluster Autoscaler for node-level scaling
- **Environments**: `dev` → `staging` → `production`
- **CI/CD**: GitHub Actions → Docker Build → Helm Deploy

---

## Technology Summary

| Layer | Technology |
|---|---|
| Runtime | .NET 8 / C# 12 |
| Web Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 + Dapper (reports) |
| Database | PostgreSQL 16 |
| Cache | Redis 7 |
| Message Broker | RabbitMQ 3.13 |
| Containerization | Docker + Docker Compose |
| Orchestration | Kubernetes + Helm |
| Auto-scaling | HPA + KEDA |
| Auth | JWT Bearer + OAuth2 |
| Observability | OpenTelemetry + Prometheus + Grafana |

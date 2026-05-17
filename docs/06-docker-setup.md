# 06 — Docker Setup

## Dockerfile — Transaction API

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FinancialBalance.Api/FinancialBalance.Api.csproj", "FinancialBalance.Api/"]
COPY ["src/FinancialBalance.Application/FinancialBalance.Application.csproj", "FinancialBalance.Application/"]
COPY ["src/FinancialBalance.Domain/FinancialBalance.Domain.csproj", "FinancialBalance.Domain/"]
COPY ["src/FinancialBalance.Infrastructure/FinancialBalance.Infrastructure.csproj", "FinancialBalance.Infrastructure/"]

RUN dotnet restore "FinancialBalance.Api/FinancialBalance.Api.csproj"

COPY src/ .
WORKDIR /src/FinancialBalance.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "FinancialBalance.Api.dll"]
```

## Dockerfile — Reporting Worker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FinancialBalance.Worker/FinancialBalance.Worker.csproj", "FinancialBalance.Worker/"]
COPY ["src/FinancialBalance.Application/FinancialBalance.Application.csproj", "FinancialBalance.Application/"]
COPY ["src/FinancialBalance.Domain/FinancialBalance.Domain.csproj", "FinancialBalance.Domain/"]
COPY ["src/FinancialBalance.Infrastructure/FinancialBalance.Infrastructure.csproj", "FinancialBalance.Infrastructure/"]

RUN dotnet restore "FinancialBalance.Worker/FinancialBalance.Worker.csproj"
COPY src/ .
WORKDIR /src/FinancialBalance.Worker
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FinancialBalance.Worker.dll"]
```

---

## docker-compose.yml (Local Development)

```yaml
version: '3.9'

services:

  transaction-api:
    build:
      context: .
      dockerfile: src/FinancialBalance.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Postgres=Host=postgres;Database=financialbalance;Username=app;Password=dev_password
      - ConnectionStrings__Redis=redis:6379
      - RabbitMQ__Host=rabbitmq
      - RabbitMQ__Username=guest
      - RabbitMQ__Password=guest
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy

  reporting-api:
    build:
      context: .
      dockerfile: src/FinancialBalance.ReportingApi/Dockerfile
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Postgres=Host=postgres;Database=financialbalance;Username=app;Password=dev_password
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy

  reporting-worker:
    build:
      context: .
      dockerfile: src/FinancialBalance.Worker/Dockerfile
    environment:
      - DOTNET_ENVIRONMENT=Development
      - ConnectionStrings__Postgres=Host=postgres;Database=financialbalance;Username=app;Password=dev_password
      - RabbitMQ__Host=rabbitmq
      - RabbitMQ__Username=guest
      - RabbitMQ__Password=guest
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: financialbalance
      POSTGRES_USER: app
      POSTGRES_PASSWORD: dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infra/sql/init.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U app -d financialbalance"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"   # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

---

## .dockerignore

```
**/.git
**/.vs
**/bin
**/obj
**/*.user
**/node_modules
**/.env
**/appsettings.Development.json
```

---

## Useful Commands

```bash
# Start all services
docker compose up -d

# Rebuild and restart a specific service
docker compose up -d --build transaction-api

# Run EF Core migrations inside container
docker compose run --rm transaction-api dotnet ef database update

# View logs
docker compose logs -f transaction-api

# Stop and remove volumes
docker compose down -v
```

---

## Image Tagging Strategy

```
financialbalance/transaction-api:latest
financialbalance/transaction-api:1.2.0
financialbalance/transaction-api:1.2.0-sha-abc1234
```

CI pipeline tags with semantic version + git SHA for full traceability.

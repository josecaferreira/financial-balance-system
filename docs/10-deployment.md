# 10 — Deployment Guide

## Environments

| Environment | Purpose | Cluster |
|---|---|---|
| `dev` | Local development | docker-compose |
| `staging` | Integration testing, QA | Kubernetes (reduced resources) |
| `production` | Live system | Kubernetes (full HA) |

---

## Kubernetes Manifests

### Transaction API Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: transaction-api
  namespace: financial-balance
spec:
  replicas: 2
  selector:
    matchLabels:
      app: transaction-api
  template:
    metadata:
      labels:
        app: transaction-api
    spec:
      containers:
        - name: transaction-api
          image: financialbalance/transaction-api:1.0.0
          ports:
            - containerPort: 8080
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ConnectionStrings__Postgres
              valueFrom:
                secretKeyRef:
                  name: financial-balance-secrets
                  key: postgres-connection-string
            - name: ConnectionStrings__Redis
              valueFrom:
                secretKeyRef:
                  name: financial-balance-secrets
                  key: redis-connection-string
          resources:
            requests:
              cpu: "250m"
              memory: "256Mi"
            limits:
              cpu: "1000m"
              memory: "512Mi"
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 5
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 10
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
            - weight: 100
              podAffinityTerm:
                labelSelector:
                  matchExpressions:
                    - key: app
                      operator: In
                      values: [transaction-api]
                topologyKey: kubernetes.io/hostname
---
apiVersion: v1
kind: Service
metadata:
  name: transaction-api
  namespace: financial-balance
spec:
  selector:
    app: transaction-api
  ports:
    - port: 80
      targetPort: 8080
```

### Ingress

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: financial-balance-ingress
  namespace: financial-balance
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - api.financialbalance.com
        - reports.financialbalance.com
      secretName: financial-balance-tls
  rules:
    - host: api.financialbalance.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: transaction-api
                port:
                  number: 80
    - host: reports.financialbalance.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: reporting-api
                port:
                  number: 80
```

---

## Helm Chart Structure

```
helm/
└── financial-balance/
    ├── Chart.yaml
    ├── values.yaml
    ├── values.staging.yaml
    ├── values.production.yaml
    └── templates/
        ├── transaction-api/
        │   ├── deployment.yaml
        │   ├── service.yaml
        │   └── hpa.yaml
        ├── reporting-api/
        │   ├── deployment.yaml
        │   ├── service.yaml
        │   └── hpa.yaml
        ├── reporting-worker/
        │   ├── deployment.yaml
        │   └── scaledobject.yaml
        ├── ingress.yaml
        ├── pdb.yaml
        └── secrets.yaml
```

### Deploy with Helm

```bash
# Staging
helm upgrade --install financial-balance ./helm/financial-balance \
  -f helm/financial-balance/values.staging.yaml \
  --namespace financial-balance \
  --create-namespace \
  --set image.tag=1.2.0

# Production
helm upgrade --install financial-balance ./helm/financial-balance \
  -f helm/financial-balance/values.production.yaml \
  --namespace financial-balance \
  --atomic \
  --timeout 5m \
  --set image.tag=1.2.0
```

---

## CI/CD Pipeline (GitHub Actions)

```yaml
# .github/workflows/deploy.yml
name: Build & Deploy

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet test --configuration Release --logger trx

  build:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build and push Docker images
        run: |
          docker build -t financialbalance/transaction-api:${{ github.sha }} \
            -f src/FinancialBalance.Api/Dockerfile .
          docker push financialbalance/transaction-api:${{ github.sha }}

  scan:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - name: Trivy vulnerability scan
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: financialbalance/transaction-api:${{ github.sha }}
          exit-code: '1'
          severity: 'CRITICAL,HIGH'

  deploy-staging:
    needs: scan
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - name: Deploy to staging
        run: |
          helm upgrade --install financial-balance ./helm/financial-balance \
            -f helm/financial-balance/values.staging.yaml \
            --set image.tag=${{ github.sha }}

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production   # requires manual approval
    if: github.ref == 'refs/heads/main'
    steps:
      - name: Deploy to production
        run: |
          helm upgrade --install financial-balance ./helm/financial-balance \
            -f helm/financial-balance/values.production.yaml \
            --set image.tag=${{ github.sha }} \
            --atomic
```

---

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "postgres", tags: ["ready"])
    .AddRedis(redisConnection, name: "redis", tags: ["ready"])
    .AddRabbitMQ(rabbitMqUri, name: "rabbitmq", tags: ["ready"]);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false   // liveness: just confirm process is running
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

---

## Rollback

```bash
# List releases
helm history financial-balance -n financial-balance

# Rollback to previous release
helm rollback financial-balance -n financial-balance

# Rollback to specific revision
helm rollback financial-balance 3 -n financial-balance
```

---

## Database Migrations

EF Core migrations run as a Kubernetes Job before each deployment:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: db-migrate-{{ .Release.Revision }}
spec:
  template:
    spec:
      containers:
        - name: migrate
          image: financialbalance/transaction-api:{{ .Values.image.tag }}
          command: ["dotnet", "ef", "database", "update"]
          env:
            - name: ConnectionStrings__Postgres
              valueFrom:
                secretKeyRef:
                  name: financial-balance-secrets
                  key: postgres-connection-string
      restartPolicy: Never
  backoffLimit: 3
```

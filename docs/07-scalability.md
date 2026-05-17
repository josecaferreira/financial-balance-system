# 07 — Auto-Scalability Strategy

## Architecture Overview

```
Internet
   │
   ▼
Cloud Load Balancer (AWS ALB / GCP GLB)
   │
   ▼
Ingress Controller (NGINX Ingress)
   │
   ├──────────────────────┐
   ▼                      ▼
Transaction API        Reporting API
(HPA: 2–20 pods)       (HPA: 2–10 pods)
   │                      │
   ▼                      ▼
RabbitMQ Queue         Redis Cache
   │
   ▼
Reporting Worker
(KEDA: 1–10 pods, queue-depth driven)
   │
   ▼
PostgreSQL (Primary + Read Replicas)
```

---

## Horizontal Pod Autoscaler (HPA)

HPA scales API pods based on CPU and memory metrics from the Kubernetes Metrics Server.

### Transaction API

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: transaction-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: transaction-api
  minReplicas: 2
  maxReplicas: 20
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
        - type: Pods
          value: 4
          periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
        - type: Pods
          value: 1
          periodSeconds: 120
```

### Reporting API

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: reporting-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: reporting-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 60
```

---

## KEDA — Event-Driven Autoscaling for Worker

The Reporting Worker scales based on RabbitMQ queue depth — the correct signal for message-processing workloads.

```yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: reporting-worker-scaler
spec:
  scaleTargetRef:
    name: reporting-worker
  minReplicaCount: 1
  maxReplicaCount: 10
  cooldownPeriod: 60
  triggers:
    - type: rabbitmq
      metadata:
        protocol: amqp
        queueName: transaction-events
        mode: QueueLength
        value: "10"       # scale up when > 10 messages per pod
      authenticationRef:
        name: rabbitmq-trigger-auth
```

**Scaling logic:** 1 additional pod per 10 messages in the queue, up to 10 pods.

---

## Cluster Autoscaler

Node-level scaling is handled by the cloud provider's Cluster Autoscaler:

```yaml
# Node group config (AWS EKS example)
nodeGroups:
  - name: app-nodes
    instanceType: m6i.xlarge   # 4 vCPU, 16 GB
    minSize: 3
    maxSize: 20
    labels:
      role: app
  - name: worker-nodes
    instanceType: c6i.large    # 2 vCPU, 4 GB — CPU-optimized
    minSize: 1
    maxSize: 10
    labels:
      role: worker
```

---

## Resource Requests & Limits

```yaml
# Transaction API
resources:
  requests:
    cpu: "250m"
    memory: "256Mi"
  limits:
    cpu: "1000m"
    memory: "512Mi"

# Reporting API
resources:
  requests:
    cpu: "200m"
    memory: "256Mi"
  limits:
    cpu: "800m"
    memory: "512Mi"

# Reporting Worker
resources:
  requests:
    cpu: "200m"
    memory: "128Mi"
  limits:
    cpu: "500m"
    memory: "256Mi"
```

---

## Pod Disruption Budget

Ensures at least 1 replica is available during rolling updates and node drains:

```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: transaction-api-pdb
spec:
  minAvailable: 1
  selector:
    matchLabels:
      app: transaction-api
```

---

## Scaling Targets Summary

| Component | Min | Max | Trigger | Scale-up Signal |
|---|---|---|---|---|
| Transaction API | 2 | 20 | HPA | CPU > 70% |
| Reporting API | 2 | 10 | HPA | CPU > 60% |
| Reporting Worker | 1 | 10 | KEDA | >10 queue messages/pod |
| Kubernetes Nodes | 3 | 20 | Cluster Autoscaler | Pod pending |

---

## Database Scaling

| Component | Strategy |
|---|---|
| PostgreSQL writes | Single primary (vertical scale first, then Citus for sharding) |
| PostgreSQL reads | Streaming read replicas (1 in dev, 2+ in prod) |
| Redis | Redis Cluster with 3 shards in production |
| RabbitMQ | Mirrored queues across 3 nodes |

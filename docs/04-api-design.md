# 04 — API Design

## Base URLs

| Service | Base URL |
|---|---|
| Transaction API | `https://api.financialbalance.com/v1` |
| Reporting API | `https://reports.financialbalance.com/v1` |

All responses use `application/json`. Dates use ISO 8601 (`2024-01-15T10:30:00Z`). Monetary values use `decimal` strings to preserve precision.

---

## Authentication

All endpoints require a JWT Bearer token:
```
Authorization: Bearer <token>
```

---

## Transaction API

### Accounts

#### `POST /accounts`
Create a new account.

**Request:**
```json
{
  "name": "Main Operating Account",
  "code": "MAIN-001",
  "type": "Checking",
  "currency": "BRL"
}
```

**Response `201 Created`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Main Operating Account",
  "code": "MAIN-001",
  "type": "Checking",
  "currency": "BRL",
  "currentBalance": "0.00",
  "isActive": true,
  "createdAt": "2024-01-15T10:00:00Z"
}
```

#### `GET /accounts`
List all accounts.

**Query params:** `page`, `pageSize`, `isActive`

#### `GET /accounts/{id}`
Get account by ID.

#### `GET /accounts/{id}/balance`
Get current balance for account.

**Response `200 OK`:**
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "currentBalance": "15420.50",
  "currency": "BRL",
  "asOf": "2024-01-15T10:30:00Z"
}
```

---

### Transactions

#### `POST /transactions`
Register a new transaction.

**Request:**
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "Incoming",
  "amount": "5000.00",
  "description": "Client payment - Invoice #1042",
  "category": "Revenue",
  "referenceNumber": "INV-1042",
  "transactionDate": "2024-01-15"
}
```

**Response `201 Created`:**
```json
{
  "id": "7b3e2c1a-9d4f-4a2b-8e5c-1f6a3b7c9d2e",
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "Incoming",
  "amount": "5000.00",
  "description": "Client payment - Invoice #1042",
  "category": "Revenue",
  "referenceNumber": "INV-1042",
  "status": "Confirmed",
  "transactionDate": "2024-01-15",
  "createdAt": "2024-01-15T10:35:00Z"
}
```

#### `GET /transactions`
List transactions with filters.

**Query params:**
| Param | Type | Description |
|---|---|---|
| `accountId` | GUID | Filter by account |
| `type` | string | `Incoming` or `Outgoing` |
| `category` | string | Transaction category |
| `from` | date | Start date (inclusive) |
| `to` | date | End date (inclusive) |
| `status` | string | `Pending`, `Confirmed`, `Cancelled` |
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Items per page (default: 20, max: 100) |

**Response `200 OK`:**
```json
{
  "data": [ /* array of transactions */ ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 143,
    "totalPages": 8
  }
}
```

#### `GET /transactions/{id}`
Get transaction by ID.

#### `PATCH /transactions/{id}/cancel`
Cancel a transaction.

**Response `200 OK`:** Updated transaction with `status: "Cancelled"`.

---

## Reporting API

### Daily Reports

#### `GET /reports/daily`
Get daily balance summary.

**Query params:**
| Param | Type | Required | Description |
|---|---|---|---|
| `accountId` | GUID | Yes | Account to report on |
| `date` | date | Yes | Date (`2024-01-15`) |

**Response `200 OK`:**
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "date": "2024-01-15",
  "totalIncoming": "12500.00",
  "totalOutgoing": "3200.00",
  "netBalance": "9300.00",
  "transactionCount": 8,
  "breakdown": [
    { "category": "Revenue", "incoming": "10000.00", "outgoing": "0.00" },
    { "category": "Supplier", "incoming": "0.00", "outgoing": "2500.00" }
  ],
  "computedAt": "2024-01-15T23:59:00Z"
}
```

#### `GET /reports/daily/range`
Get daily summaries for a date range.

**Query params:** `accountId`, `from`, `to` (max range: 92 days)

---

### Monthly Reports

#### `GET /reports/monthly`
Get monthly balance summary.

**Query params:**
| Param | Type | Required |
|---|---|---|
| `accountId` | GUID | Yes |
| `year` | int | Yes |
| `month` | int | Yes (1–12) |

**Response `200 OK`:**
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "year": 2024,
  "month": 1,
  "openingBalance": "6120.50",
  "closingBalance": "21420.50",
  "totalIncoming": "45000.00",
  "totalOutgoing": "29700.00",
  "netBalance": "15300.00",
  "transactionCount": 87,
  "dailyBreakdown": [
    {
      "date": "2024-01-01",
      "incoming": "0.00",
      "outgoing": "1200.00",
      "net": "-1200.00"
    }
    // ...
  ],
  "categoryBreakdown": [
    { "category": "Revenue", "incoming": "40000.00", "outgoing": "0.00" },
    { "category": "Payroll", "incoming": "0.00", "outgoing": "18000.00" }
  ],
  "computedAt": "2024-01-31T23:59:00Z"
}
```

#### `GET /reports/monthly/range`
Get multiple months summary.

**Query params:** `accountId`, `from` (`2024-01`), `to` (`2024-06`) (max: 12 months)

---

## Error Responses

All errors follow RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Amount must be greater than zero.",
  "instance": "/v1/transactions",
  "errors": {
    "amount": ["Amount must be greater than zero."]
  }
}
```

| Status | Meaning |
|---|---|
| `400` | Validation error |
| `401` | Unauthorized (missing/invalid token) |
| `403` | Forbidden (insufficient permissions) |
| `404` | Resource not found |
| `409` | Conflict (e.g. duplicate reference number) |
| `422` | Business rule violation |
| `429` | Rate limit exceeded |
| `500` | Internal server error |

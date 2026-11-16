# REST API Design Specification

## 1. Conventions
- **Base Route:** `/api/v1`
- **Tenant Header:** `X-Tenant-Id: <organization-uuid>` (or active tenant claim in JWT).
- **Idempotency Header:** `Idempotency-Key: <unique-key>` supported on order creation, order confirmation, fulfillment, PO goods receipt, inventory adjustments, and stock transfers.
- **Error Format:** RFC 9457 `ProblemDetails` with `errorCode`, `correlationId`, and validation `errors` dictionary.
- **Pagination Contract:** `pageNumber` (default `1`), `pageSize` (default `20`, max `100`), `search`, `sortBy`, `sortDirection`.

## 2. Security Rules
- Never expose EF Core entities directly; all endpoints accept and return explicit DTOs.
- Server recalculates all order totals, line totals, and moving average costs from authoritative database prices.
- Every endpoint requires authentication (except `/api/v1/auth/login`, `/api/v1/auth/register`, `/api/v1/auth/refresh`, and `/health/*`) and enforces fine-grained permission policies.

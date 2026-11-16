# Architecture Decision Records (ADRs)

## ADR-001: Modular Monolith with Clean Architecture over Microservices
- **Status:** Accepted
- **Context:** Inventory reservation, order confirmation, goods receiving, and audit logging require strict ACID transaction boundaries across catalog, warehouse, inventory, and order domains.
- **Decision:** Build the backend as a Clean Architecture Modular Monolith (`Domain`, `Application`, `Infrastructure`, `API`) with strict feature boundaries verified by automated architecture tests.
- **Consequences:** Eliminates distributed transaction complexity while preserving modular boundaries for future service extraction if required.

---

## ADR-002: Shared Database, Shared Schema Multi-Tenancy with 4-Layer Isolation
- **Status:** Accepted
- **Context:** Multiple organizations need isolated workspaces with low operational overhead and seamless schema evolution.
- **Decision:** Use a shared PostgreSQL database and shared schema with mandatory `tenant_id` columns, composite tenant-leading indexes, middleware membership verification, EF Core global query filters, and a write-enforcement interceptor.
- **Consequences:** High resource efficiency and strong cross-tenant IDOR protection verified via multi-tenant integration tests.

---

## ADR-003: Hybrid Materialized Stock Balance + Immutable Stock Movement Ledger
- **Status:** Accepted
- **Context:** High-concurrency order processing requires fast balance lookups and row locking, while enterprise compliance requires an immutable explanation of how every stock balance was reached.
- **Decision:** Maintain materialized balances in `inventory_items` alongside an append-only `stock_movements` ledger mutated atomically within the same transaction.
- **Consequences:** Guarantees $O(1)$ balance reads/locks and 100% auditable stock history with automated reconciliation verification.

---

## ADR-004: Pessimistic Row-Level Locking (`FOR UPDATE`) + `xmin` Concurrency + DB Check Constraints
- **Status:** Accepted
- **Context:** Simultaneous requests reserving or transferring the last available units of a SKU can cause race conditions and negative inventory if checked only in application memory.
- **Decision:** Acquire PostgreSQL `SELECT ... FOR UPDATE` row locks in deterministic `(warehouse_id, product_id)` order inside transactions, backed by `xmin` concurrency tokens and database `CHECK` constraints (`reserved_quantity <= on_hand_quantity`, `on_hand_quantity >= 0`).
- **Consequences:** Prevents overselling and deadlocks under concurrent load.

---

## ADR-005: PostgreSQL-Backed Idempotency & Hosted Background Services over Redis/Brokers
- **Status:** Accepted
- **Context:** Infrastructure complexity should be justified by genuine operational requirements rather than portfolio decoration.
- **Decision:** Persist `IdempotencyRecord` entries in PostgreSQL within the same ACID transaction as the business operation, and execute scheduled low-stock and reconciliation jobs via ASP.NET Core `BackgroundService`.
- **Consequences:** Zero risk of cache-to-database split brain on idempotency keys and simpler container operations.

---

## ADR-006: Target Framework Alignment (`net9.0` Host SDK & Docker Parity)
- **Status:** Accepted
- **Context:** Host environment inspection showed `.NET SDK 9.0.300` installed locally (`C:\Program Files\dotnet\sdk`) alongside Docker `29.9.3`.
- **Decision:** Target `net9.0` and `EF Core 9.0` across all backend and test projects so that local CLI builds, unit tests, architecture tests, integration tests, and multi-stage Docker builds execute with 100% parity and zero host SDK conflicts.
- **Consequences:** Immediate native execution on both the developer host and Docker containers with modern C# 13 and EF Core 9 capabilities.

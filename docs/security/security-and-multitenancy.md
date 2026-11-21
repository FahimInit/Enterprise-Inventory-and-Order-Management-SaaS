# Security & Multi-Tenancy Architecture

## 1. Defense-in-Depth Multi-Tenant Isolation

Global query filters alone are never relied upon as the sole tenant boundary. We enforce **4 independent layers of tenant isolation**:

```mermaid
flowchart LR
    Req["Incoming HTTP Request<br/>(JWT + X-Tenant-Id)"]
    L1["Layer 1: TenantResolutionMiddleware<br/>Verify OrganizationMember IsActive = true<br/>& load effective permissions"]
    L2["Layer 2: Application Service Scoping<br/>Validate all FKs belong to<br/>ITenantContext.TenantId"]
    L3["Layer 3: EF Core Global Query Filter<br/>e => e.TenantId == _tenantContext.TenantId"]
    L4["Layer 4: TenantWriteEnforcementInterceptor<br/>Abort SaveChangesAsync on empty<br/>or mismatched TenantId"]
    DB[("PostgreSQL 17")]

    Req --> L1 --> L2 --> L3 --> L4 --> DB
```

1. **Layer 1 — Tenant Resolution & Membership Verification Middleware:**
   - Resolves `TenantId` from `X-Tenant-Id` header or the authenticated user's active `tenant_id` JWT claim.
   - Verifies that the authenticated user holds an active `OrganizationMember` record (`IsActive = true`) in that organization and loads their effective permissions for that tenant.
2. **Layer 2 — Explicit Application Service Scoping:**
   - Application services explicitly filter and validate foreign keys (such as `CategoryId`, `WarehouseId`, `SupplierId`, `ProductId`) against `ITenantContext.TenantId`.
3. **Layer 3 — EF Core Global Query Filters:**
   - Every entity implementing `ITenantEntity` has a global query filter `e => e.TenantId == _tenantContext.TenantId`.
4. **Layer 4 — `TenantWriteEnforcementInterceptor` on `SaveChangesAsync`:**
   - Before any `INSERT`, `UPDATE`, or `DELETE` is sent to PostgreSQL, the interceptor inspects `ChangeTracker.Entries<ITenantEntity>()` and aborts the transaction if any entity has an empty or mismatched `TenantId`.

---

## 2. Authentication & Token Security

- **Access Tokens:** Short-lived HMAC-SHA256 JWTs containing `sub` (`UserId`), `email`, `tenant_id`, and `role` claims.
- **Refresh Token Rotation & Reuse Detection:** Refresh tokens are hashed (`SHA-256`) before storage and grouped by `FamilyId`. If a previously rotated refresh token is presented again, the entire token family is immediately revoked to protect against token theft.

```mermaid
sequenceDiagram
    actor Client
    participant API as ASP.NET Core API
    participant DB as PostgreSQL (RefreshTokens)

    Client->>API: POST /api/v1/auth/refresh (Token T1)
    API->>DB: Lookup SHA-256(T1)
    alt T1 is Active and Unused
        API->>DB: Mark T1 ReplacedBy T2, Insert T2 (same FamilyId)
        API-->>Client: 200 OK (New Access JWT + Refresh Token T2)
    else T1 was Already Revoked / Rotated (Token Theft Detected)
        API->>DB: Revoke ALL tokens matching FamilyId
        API-->>Client: 401 Unauthorized (Session Family Terminated)
    end
```

---

## 3. Audit Trail Protection
- All critical mutations write append-only `AuditLog` entries capturing `TenantId`, `ActorUserId`, `Action`, `EntityType`, `EntityId`, `BeforeJson`, `AfterJson`, `CorrelationId`, and `OccurredAtUtc`.
- No API endpoint or application service permits modifying or deleting audit log entries.

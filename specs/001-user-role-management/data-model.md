# Data Model: User and Role Management (M-10)

**Feature**: 001-user-role-management
**Date**: 2026-06-08

All tenant-schema tables follow DB-02 (no `tenant_id` column; isolation is at the schema level). Control-plane tables carry explicit `tenant_id` FK columns referencing `tenants.id` (DB-02 exemption, same as M-18/M-19).

---

## Tenant-Schema Tables

### `tenant_users`

Primary entity representing a user within a tenant boundary.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `user_id` | `uuid` | PK, not null | Generated on creation |
| `username` | `varchar(254)` | unique, not null | Email address; unique within the schema |
| `password_hash` | `varchar(72)` | not null | bcrypt hash (cost 12); never stored in plaintext |
| `is_mfa_enrolled` | `boolean` | not null, default false | True after first TOTP enrollment |
| `mfa_secret_encrypted` | `bytea` | nullable | Envelope-encrypted TOTP secret (AES-256-GCM) |
| `mfa_secret_key_ref` | `varchar(512)` | nullable | KMS key ID (SaaS) or config key name (on-prem) |
| `last_used_totp_step` | `bigint` | nullable | UNIX epoch step number of last accepted TOTP code (anti-replay) |
| `persona` | `varchar(16)` | not null | `P-01`..`P-08` |
| `status` | `varchar(32)` | not null, default 'active' | `active` \| `inactive` \| `locked` \| `pending-enrollment` |
| `failed_attempt_count` | `smallint` | not null, default 0 | Reset to 0 on successful auth |
| `locked_until_utc` | `timestamptz` | nullable | Populated on lockout; null when active |
| `organization_node_id` | `uuid` | nullable, FK → `organization_hierarchy_nodes.node_id` | Hierarchy scope assignment |
| `last_permission_snapshot_version` | `bigint` | not null, default 0 | Incremented on every permission change |
| `requires_password_change` | `boolean` | not null, default false | Set to true by admin-triggered password reset |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**: `username` (unique), `status`, `organization_node_id`

**Soft delete**: `status = 'inactive'` (row retained for audit history). On right-to-erasure: `username` set to `erased_{user_id}@erased`, `password_hash` replaced with a sentinel, `mfa_secret_encrypted` nulled, `mfa_secret_key_ref` nulled.

---

### `auth_sessions`

Represents an authenticated user session.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `session_id` | `uuid` | PK, not null | |
| `user_id` | `uuid` | not null, FK → `tenant_users.user_id` | |
| `token_hash` | `bytea` | not null, unique | SHA-256 of the raw opaque token |
| `issued_at_utc` | `timestamptz` | not null | |
| `absolute_expires_at_utc` | `timestamptz` | not null | Default: `issued_at + 24h`; configurable via M-11 |
| `last_activity_at_utc` | `timestamptz` | not null | Updated on every authenticated request |
| `sliding_ttl_minutes` | `smallint` | not null | Tenant-configured sliding window (default 60 min) |
| `permission_snapshot_version` | `bigint` | not null | Version when snapshot was last built |
| `permission_snapshot` | `jsonb` | not null | Serialized `PermissionSnapshot` |
| `is_active` | `boolean` | not null, default true | Set false on logout or expiry |
| `created_at` | `timestamptz` | not null | |

**Indexes**: `token_hash` (unique, for lookup by raw token hash), `user_id` (for all-sessions-by-user queries), `is_active` partial index

**Append-only semantics**: Sessions are never updated except to set `is_active = false` and bump `last_activity_at_utc`. No UPDATE on `token_hash` or audit fields.

---

### `password_reset_tokens`

Single-use, time-limited tokens for password reset flows.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `token_id` | `uuid` | PK, not null | |
| `user_id` | `uuid` | not null, FK → `tenant_users.user_id` | |
| `token_hash` | `bytea` | not null, unique | SHA-256 of raw token |
| `expires_at_utc` | `timestamptz` | not null | Default: `issued_at + 30 min`; configurable |
| `used_at_utc` | `timestamptz` | nullable | Set on redemption; non-null = used |
| `revoked` | `boolean` | not null, default false | Admin-side revocation |
| `issued_by` | `varchar(16)` | not null | `self-service` \| `admin` |
| `issued_via` | `varchar(16)` | not null | `email` \| `sms` \| `admin-api` |
| `created_at` | `timestamptz` | not null | |

**Indexes**: `token_hash` (unique), `user_id`, `expires_at_utc`

---

### `password_reset_rate_limit_records`

Application-layer rate-limit state for self-service password reset.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `email_hash` | `bytea` | PK, not null | SHA-256(normalize(email) || tenantId) |
| `window_start_utc` | `timestamptz` | not null | Start of current 30-minute window |
| `request_count` | `smallint` | not null, default 0 | Incremented per attempt |
| `updated_at` | `timestamptz` | not null | |

---

### `permission_module_assignments`

A user's access to a DOC-02 permission module.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `assignment_id` | `uuid` | PK, not null | |
| `user_id` | `uuid` | not null, FK → `tenant_users.user_id` | |
| `module_id` | `varchar(64)` | not null | Canonical module ID from DOC-02 (e.g. `SurveyBuilder`) |
| `allowed_modes` | `varchar[]` | not null | Coarse modes: `View` \| `Manage` \| `Full` (as defined by DOC-02 per module) |
| `assigned_by` | `uuid` | not null, FK → `tenant_users.user_id` | Actor who made the assignment |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

**Unique constraint**: `(user_id, module_id)`

**Indexes**: `user_id`, `module_id`

> Note: `effectiveFrom` / `effectiveTo` excluded from Phase 1. Permissions are indefinite until revoked.

---

### `custom_authorization_rules`

Per-user fine-grained action and scope overrides beyond persona baseline.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `rule_id` | `uuid` | PK, not null | |
| `user_id` | `uuid` | not null, FK → `tenant_users.user_id` | |
| `allowed_actions` | `varchar[]` | not null | Fine-grained DOC-02 action codes (e.g. `UpdateSurvey`, `DeleteSurvey`) |
| `parameter_scope_assignments` | `jsonb` | not null, default '{}' | `{ "branch": ["Riyadh", "Dammam"] }` |
| `created_by` | `uuid` | not null, FK → `tenant_users.user_id` | |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

**Indexes**: `user_id`

> Note: `restrictedEntities` excluded from Phase 1 per spec clarification.

---

### `data_scope_assignments`

Parameter-based allowed values sourced from M-13 assigned to a user.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `assignment_id` | `uuid` | PK, not null | |
| `user_id` | `uuid` | not null, FK → `tenant_users.user_id` | |
| `parameter_name` | `varchar(128)` | not null | Must exist in `data_scope_parameter_definitions` |
| `allowed_values` | `varchar[]` | not null | Subset of the parameter's allowed values |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

**Unique constraint**: `(user_id, parameter_name)`

---

### `data_scope_parameter_definitions`

M-13-supplied parameter definitions and their allowed values.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `parameter_name` | `varchar(128)` | PK, not null | Unique within tenant schema |
| `label` | `varchar(256)` | not null | Display name (bilingual stored as jsonb or separate columns) |
| `allowed_values` | `varchar[]` | not null | All valid values for this parameter |
| `source_module` | `varchar(8)` | not null, default 'M-13' | |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

---

### `organization_hierarchy_nodes`

Tenant organisational scope nodes. Owned by M-11 (manual) or M-13 (integration); M-10 reads only.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `node_id` | `uuid` | PK, not null | |
| `parent_node_id` | `uuid` | nullable, FK → `organization_hierarchy_nodes.node_id` | null for root nodes |
| `name` | `varchar(256)` | not null | Display name |
| `path` | `varchar(2048)` | not null | Materialized path e.g. `/root/region-a/branch-x/` |
| `source` | `varchar(16)` | not null | `manual` \| `integration` |
| `external_ref` | `varchar(512)` | nullable | External ID for M-13-supplied nodes |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

**Indexes**: `path` (for `LIKE` prefix queries), `parent_node_id`

> M-10 never writes this table. It is populated by M-11 (when `hierarchySource = manual`) or M-13 (when `hierarchySource = integration`).

---

## Control-Plane Tables

### `persona_baselines`

Per-tenant default permission module assignments for each persona (P-01..P-08).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `baseline_id` | `uuid` | PK, not null | |
| `tenant_id` | `uuid` | not null, FK → `tenants.id` | Control-plane; DB-02 exemption |
| `persona_id` | `varchar(8)` | not null | `P-01`..`P-08` |
| `permission_module_assignments` | `jsonb` | not null | `[{ "moduleId": "SurveyBuilder", "allowedModes": ["View", "Manage"] }]` |
| `default_data_scope_rules` | `jsonb` | not null, default '{}' | Default scope rules for this persona |
| `is_customised` | `boolean` | not null, default false | True once tenant admin has modified the platform default |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

**Unique constraint**: `(tenant_id, persona_id)`

**Seeding**: At tenant provisioning, one row per persona (P-01..P-08) is inserted with platform defaults. No migration required for per-tenant customisation.

---

### `identity_provider_configs`

Per-tenant SSO provider configuration. Forward-compatible; no provider logic is executed in Phase 1.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `provider_id` | `uuid` | PK, not null | |
| `tenant_id` | `uuid` | not null, FK → `tenants.id` | Control-plane; DB-02 exemption |
| `provider_type` | `varchar(32)` | not null | `directory` \| `google-oidc` \| `internal` \| `saml2` \| `nafath` |
| `settings` | `jsonb` | not null | Provider-specific config (extensible, no hardcoded fields) |
| `is_active` | `boolean` | not null, default false | Only one provider may be active per tenant |
| `created_at` | `timestamptz` | not null | |
| `updated_at` | `timestamptz` | not null | |

**Unique constraint**: `(tenant_id, provider_type)` — one record per provider type per tenant

---

## Permission Snapshot (in-memory + stored in `auth_sessions.permission_snapshot`)

The permission snapshot is a compact JSON structure serialized into `auth_sessions.permission_snapshot`. It is rebuilt when `user.last_permission_snapshot_version` does not match `session.permission_snapshot_version`.

```json
{
  "version": 7,
  "modules": {
    "SurveyBuilder": ["View", "Manage"],
    "Analytics": ["View"]
  },
  "customActions": ["UpdateSurvey"],
  "scopeAssignments": {
    "branch": ["Riyadh", "Dammam"]
  },
  "hierarchyNodeId": "uuid-of-assigned-node",
  "hierarchyDescendantIds": ["uuid1", "uuid2", "uuid3"]
}
```

`hierarchyDescendantIds` is pre-computed and stored in the snapshot to avoid a tree traversal on every request. Rebuilding the snapshot re-computes descendants from `organization_hierarchy_nodes` using the materialized path query.

---

## M-10 Event Types (written to M-10's tenant-schema `event_log`)

M-10 owns this `event_log` end-to-end: it writes each event in the same transaction as the
business change (FR-015) and reads them back via `IAuditLogReader` for the Audit Log page.
(Earlier drafts routed this through an external M-17 module; that was never built and the
ownership reverted to M-10 — see gap-analysis I-02/I-04 and permissions-api.md "Audit
ownership".) The following types are emitted:

| Event Type | Trigger |
|-----------|---------|
| `user.created` | New user provisioned |
| `user.updated` | User profile changed |
| `user.deactivated` | User set to inactive |
| `user.reactivated` | Inactive user re-activated |
| `user.unlocked` | Manual unlock by admin |
| `role.assigned` | Persona changed for user |
| `role.revoked` | Persona removed |
| `permission.assigned` | `PermissionModuleAssignment` created |
| `permission.modified` | `PermissionModuleAssignment` updated |
| `permission.revoked` | `PermissionModuleAssignment` deleted |
| `session.created` | Authenticated session established |
| `session.revoked` | Session invalidated (logout or admin revoke) |
| `mfa.enrolled` | User completed TOTP enrollment |
| `mfa.reset` | Admin reset user's MFA |
| `authentication.succeeded` | Credential verification passed |
| `authentication.mfa.succeeded` | MFA challenge passed, session created |
| `authentication.mfa.failed` | MFA code rejected |
| `authentication.account.locked` | 5th consecutive failure; account locked |
| `authentication.account.unlocked` | Auto-unlock after cooldown or manual unlock |
| `password.reset.requested` | Self-service or admin password reset initiated |
| `password.reset.completed` | New password set |
| `password.reset.rate_limited` | Reset request rejected by rate limiter |
| `scope.assigned` | Data scope assignment created |
| `scope.modified` | Data scope assignment updated |
| `scope.revoked` | Data scope assignment deleted |
| `persona_baseline.updated` | Tenant admin modified a persona baseline |
| `custom_rule.created` | Custom authorization rule added |
| `custom_rule.updated` | Custom authorization rule modified |
| `custom_rule.deleted` | Custom authorization rule removed |
| `identity_provider.configured` | SSO config created or updated |

**Event payload** (all events):
```json
{
  "eventType": "permission.modified",
  "actorId": "uuid",
  "actorPersona": "P-01",
  "entityType": "PermissionModuleAssignment",
  "entityId": "uuid",
  "oldValue": { ... },
  "newValue": { ... },
  "occurredAtUtc": "2026-06-08T12:00:00Z",
  "correlationId": "uuid"
}
```

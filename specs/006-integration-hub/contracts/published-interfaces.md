# Published Interface Contracts: M-13 Integration Hub

Per architecture-constitution Article 1A rule 3, published cross-module interfaces live in
`Domain/Interfaces/`. This document lists (a) interfaces M-13 **consumes** — including two that
are **real, already-shipped** integrations, not stubs — and (b) interfaces M-13 **publishes**.

---

## Consumed — M-10 (`Nabadat.UserManagement`) — REAL, already shipped (CMC-06)

M-13 does not define its own port for this — it's a plain outbound HTTP call to an existing,
real M-10 endpoint (no in-process interface, since M-10 and M-13 are different projects hosted
in the same process but this particular contract is HTTP-shaped per M-10's own design):

```
POST /api/v1/authorization/scope/parameters
Body: { "sourceModule": "M-13", "parameters": [ { "name", "label", "allowedValues": [...] } ] }
```

Implemented by `Nabadat.UserManagement.Application.Permissions.M13ParameterContractAdapter`
(exists today). M-13's `Application/Parameters/DataScopeContractPublisher.cs` (new) calls this
whenever a filterable/mapping-enabled parameter's known value set changes (List-type parameters:
the mapping table's distinct source values; enumerable-by-nature types only — confirmed against
`M13ParameterPayload`'s shape at implementation time). Reserved names
(`user_id`/`tenant_id`/`persona`/`id`/`node_id`/`assignment_id`/`rule_id`) and the 500-
definitions-per-payload ceiling are M-10-side constraints M-13 must respect when batching.

## Consumed — M-01 (`Nabadat.SurveyBuilder`) — REAL, already shipped

**Correction to the source SRS**: CMC-02 names this dependency "M-03 Survey & Forms." Per the
constitution's Module Registry, M-03 is "Audience and Contact Management" — a different,
unrelated module. The real owner of survey definitions and rendering is **M-01 "Survey and Form
Builder"** (`Nabadat.SurveyBuilder`), which already exists (coordination-log.md C-04).

```csharp
// Nabadat.SurveyBuilder.Domain.Interfaces.ISurveyRenderService (existing, AD-01 published contract)
Task<SurveyDefinition?> GetActiveSurveyDefinitionAsync(SurveyId surveyId, LocaleCode locale, CancellationToken ct);
```

M-13 takes a direct project reference to `Nabadat.SurveyBuilder` and calls this for SCN-03 (JSON
render), once `ISurveyResolutionReader` (below) has resolved a `SurveyId`. Already consumed by
M-02 and M-04 for the same purpose per the interface's own doc comment — M-13 is a legitimate
third consumer, no new contract negotiation needed with M-01's team.

---

## Consumed — M-02 (does not exist yet) — two M-13-owned stub ports

```csharp
namespace Nabadat.IntegrationHub.Domain.Interfaces;

public interface ISurveyResolutionReader
{
    /// <summary>Resolves which survey applies for a channel + the transaction parameters
    /// received (BR-19 — M-02 rules own this for all five scenarios). Returns null when no
    /// survey resolves (surfaced as a blocking error, never a silent default).</summary>
    Task<Guid?> ResolveSurveyIdAsync(Guid serviceChannelId, IReadOnlyDictionary<string, string> parameters, CancellationToken ct = default);
}

public interface ISurveyDispatchGateway
{
    /// <summary>Hands off a resolved survey + transaction context for delivery via the
    /// suitable channel (SCN-01, CMC-01). Fire-and-forget from M-13's perspective — M-02
    /// delivery failures never surface as M-13 API errors.</summary>
    Task DispatchAsync(Guid surveyId, Guid serviceChannelId, IReadOnlyDictionary<string, string> parameters, Guid requestId, CancellationToken ct = default);
}
```

**Default adapters (shipped today)**: `NullSurveyResolutionReader` (always returns `null`,
deterministically — every scenario surfaces a clear "survey could not be resolved" internal
error rather than silently guessing); `NullSurveyDispatchGateway` (no-op, records the call was
attempted for integration-test assertions). Real adapters registered by the host once M-02
ships (coordination-log.md C-01).

---

## Consumed — M-04 (does not exist yet) — one M-13-owned stub port

```csharp
namespace Nabadat.IntegrationHub.Domain.Interfaces;

public interface IResponseIngestionGateway
{
    /// <summary>Forwards a SCN-05 payload (transaction details + survey response) for
    /// validation, dedup, and storage (CMC-03). M-04 MUST save every payload this call
    /// succeeds with, unconditionally — no discretionary rejection path (Clarifications
    /// 2026-07-27, SC-016).</summary>
    Task ForwardResponseAsync(Guid serviceChannelId, string transactionId, IReadOnlyDictionary<string, string> parameters, object surveyResponse, CancellationToken ct = default);
}
```

**Default adapter (shipped today)**: `NullResponseIngestionGateway` (no-op; integration tests
assert the call was made with the exact payload, since there's no real M-04 to verify durability
against yet). Real adapter registered by the host once M-04 ships (coordination-log.md C-02).

**Note**: the actual respondent-facing, unauthenticated, origin-checked rendering surface that
SCN-04's embed URL points at (Clarifications 2026-07-27) is **not** this gateway and is **not
built by M-13 at all** — it is owned by M-04 or a dedicated "Survey renderer" frontend
(constitution Section 1), tracked as part of coordination-log.md C-02.

---

## Published — for M-14 / M-15 / M-16 (CMC-07, forward contract only)

```csharp
namespace Nabadat.IntegrationHub.Domain.Interfaces;

public interface IParameterCatalogReader
{
    /// <summary>The tenant's enabled parameter catalogue, for future rule/action/journey
    /// builders (M-14/M-15/M-16) that may reference M-13 parameters. Referencing a parameter
    /// via this reader participates in the BR-10 impact-warning guard when that parameter is
    /// later disabled.</summary>
    Task<IReadOnlyList<ParameterCatalogEntry>> GetEnabledParametersAsync(CancellationToken ct = default);
}

public sealed record ParameterCatalogEntry(Guid Id, string NameEn, string NameAr, string ApiField, string DataType);
```

Skeleton only (mirrors M-15's `IActionOverlayReader` precedent) — no real consumer exists yet;
M-14/M-15/M-16's current data-scope needs are served through M-10 directly (via the real CMC-06
integration above), not through this reader.

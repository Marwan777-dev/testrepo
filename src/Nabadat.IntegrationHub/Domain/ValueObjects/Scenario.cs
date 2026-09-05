namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// The five integration scenarios (SCN-01…05, data-model.md §1). Exactly one is chosen per
/// <see cref="Entities.Integration"/> and it is <b>immutable after create</b> (BR-02) — changing the
/// scenario requires a new integration. Each member drives one inbound endpoint and its success
/// artifact (contracts/api-endpoints.md § Inbound Scenario API).
/// <para>Persisted as the snake_case wire value (<c>dispatch</c>, <c>redirect_link</c>,
/// <c>json_render</c>, <c>iframe_embed</c>, <c>response_ingestion</c>) via
/// <c>ScenarioConverter</c> — NOT the PascalCase member name.</para>
/// </summary>
public enum Scenario
{
    /// <summary>SCN-01 — hand the request off for survey distribution; returns <c>202</c> + a request id.</summary>
    Dispatch = 1,

    /// <summary>SCN-02 — return a survey URL the caller redirects to; expires 24h from issue by default (FR-F0-08).</summary>
    RedirectLink = 2,

    /// <summary>SCN-03 — return the survey definition JSON, relayed from M-01's <c>ISurveyRenderService</c> (research.md §4.2).</summary>
    JsonRender = 3,

    /// <summary>SCN-04 — return a short-lived embed URL; the browser loads it from a separate origin-checked surface (two-step flow).</summary>
    IframeEmbed = 4,

    /// <summary>SCN-05 — forward a completed survey response for ingestion; M-04 must save it unconditionally (CMC-03).</summary>
    ResponseIngestion = 5,
}

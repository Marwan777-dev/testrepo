using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection so the FR-10.4 low-response render tests (T158/T159) share ONE
/// <see cref="RenderPlanApplicationFactory"/> — Postgres + Elasticsearch booted once for both,
/// separate from the survey-builder API collection (only these two tests need the ES cluster).
/// </summary>
[CollectionDefinition("render-plan")]
public sealed class RenderPlanCollection : ICollectionFixture<RenderPlanApplicationFactory>;

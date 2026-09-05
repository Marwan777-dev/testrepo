using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection so the F13 Survey Report API tests (T248) share ONE
/// <see cref="ReportApplicationFactory"/> — Postgres + Elasticsearch booted once, separate from the
/// survey-builder API collection (only the report/analytics tests need the ES cluster).
/// </summary>
[CollectionDefinition("report")]
public sealed class ReportCollection : ICollectionFixture<ReportApplicationFactory>;

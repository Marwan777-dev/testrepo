using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection so every M-01 API/scenario test class shares ONE
/// <see cref="SurveyBuilderApplicationFactory"/> (one Dockerised Postgres booted once for the whole
/// integration run, not per class). Test classes opt in with <c>[Collection("survey-builder")]</c>.
/// </summary>
[CollectionDefinition("survey-builder")]
public sealed class SurveyBuilderCollection : ICollectionFixture<SurveyBuilderApplicationFactory>;

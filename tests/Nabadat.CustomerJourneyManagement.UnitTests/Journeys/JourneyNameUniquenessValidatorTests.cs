using FluentAssertions;
using Nabadat.CustomerJourneyManagement.Application.Journeys;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Journeys;

/// <summary>
/// Unit tests for <see cref="JourneyNameUniquenessValidator"/> (T017 / US-1). The validator
/// delegates the case-insensitive, Archived-excluding lookup to
/// <see cref="IJourneyDataService.ExistsActiveByNameAsync"/> and maps a hit to the
/// <c>journey.name_conflict</c> error; an Archived journey releases its name for reuse.
/// </summary>
public sealed class JourneyNameUniquenessValidatorTests
{
    private readonly IJourneyDataService _journeys = Substitute.For<IJourneyDataService>();

    private JourneyNameUniquenessValidator CreateSut() => new(_journeys);

    [Fact]
    public async Task Validate_returns_name_conflict_when_case_insensitive_duplicate_exists()
    {
        _journeys
            .ExistsActiveByNameAsync("CUSTOMER ONBOARDING", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateSut().ValidateAsync("CUSTOMER ONBOARDING");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.name_conflict");
    }

    [Fact]
    public async Task Validate_passes_when_only_an_archived_journey_holds_the_name()
    {
        // The repository excludes Archived journeys from the live-name index, so an archived
        // namesake reports "no active match" — the name is free to reuse.
        _journeys
            .ExistsActiveByNameAsync("Onboarding", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateSut().ValidateAsync("Onboarding");

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Validate_passes_when_name_is_unique_on_a_fresh_tenant()
    {
        _journeys
            .ExistsActiveByNameAsync("Brand New Journey", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateSut().ValidateAsync("Brand New Journey");

        result.IsSuccess.Should().BeTrue();
    }
}

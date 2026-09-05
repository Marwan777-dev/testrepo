using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T109 — write-first unit tests for <c>PublishAuthorizationService</c> (T115). It
/// answers "may this actor publish this survey?" by consulting
/// <see cref="IPermissionChecker"/> for the <c>PublishOwnSurveys</c> grant and comparing
/// the survey's <c>owner_user_id</c> to the caller (FR-15.5, BR-15.2). A P-03 without the
/// grant on their own Draft is Forbidden — they must submit for review first.
/// <para>Neither the service nor <see cref="IPermissionChecker"/> exists yet → the project
/// fails to COMPILE (valid red).</para>
/// </summary>
public sealed class PublishAuthorizationServiceTests
{
    private const string ProgramManager = "P-01";
    private const string SurveyAdmin = "P-03";
    private const string PublishGrant = "PublishOwnSurveys";

    private readonly IPermissionChecker _permissions = Substitute.For<IPermissionChecker>();
    private readonly Guid _author = Guid.NewGuid();

    private PublishAuthorizationService CreateSut() => new(_permissions);

    [Fact]
    public async Task Authorize_forbids_p03_without_grant_on_own_draft()
    {
        _permissions.HasGrantAsync(_author, PublishGrant, Arg.Any<CancellationToken>()).Returns(false);
        var actor = new PublishActor(SurveyAdmin, _author);
        var survey = new SurveyApprovalInfo(SurveyStatus.Draft, OwnerUserId: _author);

        var result = await CreateSut().AuthorizeAsync(actor, survey, CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be("survey.publish.forbidden");
    }

    [Fact]
    public async Task Authorize_permits_p03_with_grant_on_own_draft()
    {
        _permissions.HasGrantAsync(_author, PublishGrant, Arg.Any<CancellationToken>()).Returns(true);
        var actor = new PublishActor(SurveyAdmin, _author);
        var survey = new SurveyApprovalInfo(SurveyStatus.Draft, OwnerUserId: _author);

        var result = await CreateSut().AuthorizeAsync(actor, survey, CancellationToken.None);

        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task Authorize_forbids_p03_with_grant_when_not_the_author()
    {
        _permissions.HasGrantAsync(Arg.Any<Guid>(), PublishGrant, Arg.Any<CancellationToken>()).Returns(true);
        var actor = new PublishActor(SurveyAdmin, Guid.NewGuid()); // caller is not the owner
        var survey = new SurveyApprovalInfo(SurveyStatus.PendingReview, OwnerUserId: _author);

        var result = await CreateSut().AuthorizeAsync(actor, survey, CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be("survey.publish.forbidden");
    }

    [Fact]
    public async Task Authorize_permits_p01_without_consulting_grant()
    {
        var actor = new PublishActor(ProgramManager, Guid.NewGuid());
        var survey = new SurveyApprovalInfo(SurveyStatus.PendingReview, OwnerUserId: _author);

        var result = await CreateSut().AuthorizeAsync(actor, survey, CancellationToken.None);

        result.IsAuthorized.Should().BeTrue();
        await _permissions.DidNotReceive().HasGrantAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

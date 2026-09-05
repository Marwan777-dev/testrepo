using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T110 — write-first unit tests for <c>ReviewNotificationBuilder</c> (T116). On submit,
/// M-01 broadcasts one M-09 notification per tenant user holding <c>survey.publish</c>
/// (Q7 broadcast fan-out, FR-15.2). The builder produces the broadcast descriptor — tenant
/// scope, the gating permission, the F3 Settings deep-link, and the notification template.
/// <para><c>ReviewNotificationBuilder</c> does not exist yet → the project fails to COMPILE (valid red).</para>
/// </summary>
public sealed class ReviewNotificationBuilderTests
{
    private static ReviewNotificationBuilder CreateSut() => new();

    [Fact]
    public void Build_produces_a_tenant_scoped_publish_broadcast_deep_linking_the_settings_screen()
    {
        var surveyId = new SurveyId(Guid.NewGuid());
        var submitter = Guid.NewGuid();

        var broadcast = CreateSut().Build(surveyId, submitter);

        broadcast.Scope.Should().Be("tenant");
        broadcast.Permission.Should().Be("survey.publish");
        broadcast.DeepLink.Should().Be($"/surveys/{surveyId.Value}");
        broadcast.Template.Should().Be("survey.submitted_for_review");
    }
}

using FluentAssertions;
using Nabadat.IntegrationHub.Application.Integrations;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Integrations;

/// <summary>
/// T073 [US3] — unit tests for <c>WizardDraftDiscardPolicy</c> (BR-25 / FR-S2-01): a credential generated
/// inside the create-wizard belongs to the <b>draft</b>. Cancelling discards it with the draft, and it is
/// unusable even if a client somehow retained the plaintext.
///
/// <para>Contract these tests pin for the implementer (T081):
/// <list type="bullet">
///   <item><c>WizardDraftDiscardPolicy</c> in <c>Application/Integrations/</c> with three pure methods:
///   <c>CredentialDisposition Resolve(CredentialDraft?, WizardOutcome)</c>,
///   <c>CredentialDraft Discard(CredentialDraft)</c> and <c>bool IsUsable(CredentialDraft?)</c>.</item>
///   <item>The rule it encodes is <b>structural</b>, and that is the real enforcement: M-13 exposes no
///   endpoint that persists a credential for a not-yet-created integration. A draft credential is written
///   only as part of the committed <c>POST /integrations</c> write, so "cancel" means the server was never
///   asked and no hash exists — nothing can authenticate against it.</item>
///   <item><c>Discard</c> returns a <b>scrubbed</b> draft (empty secret, <c>Discarded = true</c>) rather than
///   mutating in place, so a discarded draft cannot be replayed into the persist path by a later caller
///   holding the original reference.</item>
/// </list></para>
/// </summary>
public sealed class WizardDraftDiscardPolicyTests
{
    private static readonly WizardDraftDiscardPolicy Policy = new();

    private static CredentialDraft Draft() =>
        new(CredentialMechanism.ApiKey, "Core Bus Key", "nbk_plaintext-secret-value", Scopes: null);

    [Fact]
    public void Resolve_discards_a_credential_generated_inside_a_cancelled_wizard()
    {
        // spec.md required case: DiscardOnCancel(generatedCredential=K1, wizardCancelled=true) → K1 is never
        // persisted/usable (BR-25).
        Policy.Resolve(Draft(), WizardOutcome.Cancelled).Should().Be(CredentialDisposition.Discard);
    }

    [Fact]
    public void Resolve_persists_the_credential_when_the_wizard_commits()
    {
        Policy.Resolve(Draft(), WizardOutcome.Committed).Should().Be(CredentialDisposition.Persist);
    }

    [Fact]
    public void Resolve_discards_when_there_is_no_draft_credential_at_all()
    {
        // "Discard" here means "nothing to write" — an integration saved without generating a credential must
        // not produce an empty credential row.
        Policy.Resolve(null, WizardOutcome.Committed).Should().Be(CredentialDisposition.Discard);
        Policy.Resolve(null, WizardOutcome.Cancelled).Should().Be(CredentialDisposition.Discard);
    }

    [Fact]
    public void Discard_scrubs_the_plaintext_and_marks_the_draft_discarded()
    {
        var discarded = Policy.Discard(Draft());

        discarded.Secret.Should().BeEmpty();
        discarded.Discarded.Should().BeTrue();
        discarded.LabelOrClientName.Should().Be("Core Bus Key");
    }

    [Fact]
    public void Discard_does_not_mutate_the_original_draft_instance()
    {
        var original = Draft();

        Policy.Discard(original);

        original.Discarded.Should().BeFalse();
        original.Secret.Should().NotBeEmpty();
    }

    [Fact]
    public void IsUsable_is_false_once_the_draft_has_been_discarded()
    {
        Policy.IsUsable(Draft()).Should().BeTrue();
        Policy.IsUsable(Policy.Discard(Draft())).Should().BeFalse();
        Policy.IsUsable(null).Should().BeFalse();
    }

    [Fact]
    public void Resolve_refuses_to_persist_a_draft_that_was_already_discarded()
    {
        // Defence in depth: even a "Committed" outcome cannot resurrect a scrubbed draft — its secret is gone,
        // so persisting it would store a hash of an empty string that no caller could ever present.
        var discarded = Policy.Discard(Draft());

        Policy.Resolve(discarded, WizardOutcome.Committed).Should().Be(CredentialDisposition.Discard);
    }
}

using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.IntegrationHub.Application.Integrations;
using Nabadat.IntegrationHub.Application.Integrations.Dtos;
using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Integrations;

/// <summary>
/// T070 [US3] — unit tests for <c>ApiKeyGenerationService</c> (BR-16 / FR-S2-05 / NFR-6): generating an API
/// key returns the plaintext <b>exactly once</b>, stores only a hash, and implicitly revokes the integration's
/// prior Active key in the same operation — no separate confirmation, no second user action.
///
/// <para>Contract these tests pin for the implementer (T078):
/// <list type="bullet">
///   <item><c>ApiKeyGenerationService(CredentialSecretHasher, CredentialRevocationService, TimeProvider)</c> in
///   <c>Application/Integrations/</c>, with one pure in-memory method
///   <c>CredentialGenerationResult Generate(CredentialGenerateCommand command, Credential? currentActive)</c>.
///   It builds entities and never touches the database — <c>IntegrationService</c> (T082) persists them, which
///   is what keeps BR-16's revoke+issue pair a single atomic write.</item>
///   <item>The plaintext lives <b>only</b> on <c>GeneratedCredential.Secret</c>, the show-once channel. It is
///   never assigned to <see cref="Credential.SecretHash"/> and never re-derivable from the stored row.</item>
///   <item>Supersession is delegated to <c>CredentialRevocationService</c> so the revoked row is stamped
///   identically whether it was superseded (here) or revoked standalone (US8).</item>
///   <item>An <c>api_key</c> credential carries <c>Scopes = null</c> — the baseline's
///   <c>ck_credentials_scopes_mechanism</c> CHECK allows scopes only on <c>oauth_client</c>.</item>
/// </list></para>
/// </summary>
public sealed class ApiKeyGenerationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid IntegrationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeTimeProvider _time = new(Now);
    private readonly CredentialSecretHasher _hasher = new();

    private ApiKeyGenerationService CreateService() =>
        new(_hasher, new CredentialRevocationService(_time), _time);

    private static CredentialGenerateCommand Command(string? label = "Core Bus Key") =>
        new(IntegrationId, CredentialMechanism.ApiKey, label, Scopes: null, ActorId);

    [Fact]
    public void Generate_returns_the_plaintext_once_and_stores_only_its_hash()
    {
        // spec.md required case: Generate(keyLabel="Core Bus Key") → returns plaintext once; the stored value
        // is not equal to the plaintext (hashed), and the plaintext is not recoverable from the row.
        var result = CreateService().Generate(Command(), currentActive: null);

        result.Succeeded.Should().BeTrue();
        var generated = result.Generated!;

        generated.Secret.Should().NotBeNullOrWhiteSpace();
        generated.Credential.SecretHash.Should().NotBe(generated.Secret);
        generated.Credential.SecretHash.Should().NotContain(generated.Secret);
        _hasher.Verify(generated.Secret, generated.Credential.SecretHash).Should().BeTrue();
    }

    [Fact]
    public void Generate_produces_a_high_entropy_secret_that_never_repeats()
    {
        var service = CreateService();

        var first = service.Generate(Command(), currentActive: null).Generated!;
        var second = service.Generate(Command("Second key"), currentActive: null).Generated!;

        first.Secret.Should().NotBe(second.Secret);
        first.Secret.Length.Should().BeGreaterThanOrEqualTo(32);
        first.Credential.SecretHash.Should().NotBe(second.Credential.SecretHash);
    }

    [Fact]
    public void Generate_stamps_the_credential_as_an_active_api_key_with_no_scopes()
    {
        var generated = CreateService().Generate(Command(), currentActive: null).Generated!;

        generated.Credential.IntegrationId.Should().Be(IntegrationId);
        generated.Credential.Mechanism.Should().Be(CredentialMechanism.ApiKey);
        generated.Credential.LabelOrClientName.Should().Be("Core Bus Key");
        generated.Credential.Status.Should().Be(CredentialStatus.Active);
        generated.Credential.GeneratedAt.Should().Be(Now);
        generated.Credential.GeneratedBy.Should().Be(ActorId);
        generated.Credential.RevokedAt.Should().BeNull();

        // ck_credentials_scopes_mechanism: scopes exist only for oauth_client.
        generated.Credential.Scopes.Should().BeNull();

        // client_id is an OAuth concept only.
        generated.ClientId.Should().BeNull();
    }

    [Fact]
    public void Generate_implicitly_revokes_the_prior_active_key_and_activates_the_new_one()
    {
        // spec.md required case: Generate(existingActiveKey=K1, newLabel="K2") → K1 is implicitly revoked,
        // K2 becomes active (BR-16) — one operation, no separate confirmation for K1.
        var k1 = ActiveKey("K1");

        var result = CreateService().Generate(Command("K2"), currentActive: k1);

        var generated = result.Generated!;
        generated.Superseded.Should().BeSameAs(k1);
        k1.Status.Should().Be(CredentialStatus.Revoked);
        k1.RevokedAt.Should().Be(Now);

        generated.Credential.Status.Should().Be(CredentialStatus.Active);
        generated.Credential.LabelOrClientName.Should().Be("K2");
        generated.Credential.Id.Should().NotBe(k1.Id);
    }

    [Fact]
    public void Generate_reports_no_superseded_credential_when_the_integration_has_none()
    {
        CreateService().Generate(Command(), currentActive: null).Generated!.Superseded.Should().BeNull();
    }

    [Fact]
    public void Generate_leaves_an_already_revoked_credential_untouched()
    {
        // A caller that passes a revoked row (rather than null) must not have its revoked_at rewritten — the
        // audit trail records when the key actually stopped working.
        var revokedEarlier = ActiveKey("Old");
        revokedEarlier.Status = CredentialStatus.Revoked;
        revokedEarlier.RevokedAt = Now.AddDays(-3);

        CreateService().Generate(Command("New"), currentActive: revokedEarlier);

        revokedEarlier.RevokedAt.Should().Be(Now.AddDays(-3));
    }

    [Fact]
    public void Generate_returns_invalid_key_label_required_when_the_label_is_missing()
    {
        // VR-F10 — key label is required; the wizard blocks step advance on it.
        var result = CreateService().Generate(Command(label: "  "), currentActive: null);

        result.Succeeded.Should().BeFalse();
        result.Generated.Should().BeNull();
        result.Errors.Single().Code.Should().Be(IntegrationErrorCodes.KeyLabelRequired);
        result.Errors.Single().Field.Should().Be(IntegrationFields.KeyLabel);
    }

    [Fact]
    public void Generate_does_not_revoke_the_current_key_when_the_request_is_invalid()
    {
        // A rejected generation must leave the integration authenticating exactly as before — otherwise a
        // typo in the label would take a live caller offline.
        var k1 = ActiveKey("K1");

        CreateService().Generate(Command(label: null), currentActive: k1).Succeeded.Should().BeFalse();

        k1.Status.Should().Be(CredentialStatus.Active);
        k1.RevokedAt.Should().BeNull();
    }

    private static Credential ActiveKey(string label) => new()
    {
        Id = Guid.NewGuid(),
        IntegrationId = IntegrationId,
        Mechanism = CredentialMechanism.ApiKey,
        LabelOrClientName = label,
        SecretHash = "existing-hash",
        Status = CredentialStatus.Active,
        GeneratedAt = Now.AddDays(-10),
        GeneratedBy = ActorId,
    };
}

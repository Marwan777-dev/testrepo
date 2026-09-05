using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.IntegrationHub.Application.Integrations;
using Nabadat.IntegrationHub.Application.Integrations.Dtos;
using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Integrations;

/// <summary>
/// T071 [US3] — unit tests for <c>OAuthClientGenerationService</c> (BR-17 / BR-26 / FR-S2-06): generating an
/// OAuth client returns a show-once <c>client_secret</c>, stores only its hash, applies the selected scopes,
/// and pins the grant type and access-token lifetime <b>in code</b> — neither is an input, a column, or a
/// console field (ratified removals, <c>[PO-G13]</c>).
///
/// <para>Contract these tests pin for the implementer (T079):
/// <list type="bullet">
///   <item><c>OAuthClientGenerationService(CredentialSecretHasher, CredentialRevocationService, TimeProvider)</c>
///   with <c>CredentialGenerationResult Generate(CredentialGenerateCommand, Credential? currentActive)</c> —
///   the same signature as the API-key service, so <c>IntegrationService</c> composes them interchangeably.</item>
///   <item><c>GrantType</c> and <c>AccessTokenLifetime</c> are <b>constants on the type</b>. There is no
///   parameter, no overload and no configuration key that can change them.</item>
///   <item>The <c>client_id</c> is the credential row's own id — data-model.md §2 defines no separate column,
///   and inventing one would put a second identifier out of sync with the row it names.</item>
///   <item>Scopes are validated against BR-26's ratified five and normalised (trimmed, de-duplicated, stored
///   in catalogue order) so a token's scope set is comparable byte-for-byte at authentication time.</item>
/// </list></para>
/// </summary>
public sealed class OAuthClientGenerationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid IntegrationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly FakeTimeProvider _time = new(Now);
    private readonly CredentialSecretHasher _hasher = new();

    private OAuthClientGenerationService CreateService() =>
        new(_hasher, new CredentialRevocationService(_time), _time);

    private static CredentialGenerateCommand Command(
        string? clientName = "Core Bus Client",
        params string[] scopes) =>
        new(IntegrationId, CredentialMechanism.OAuthClient, clientName,
            scopes.Length == 0 ? new[] { OAuthScopes.SurveyRequestsWrite } : scopes, ActorId);

    [Fact]
    public void GrantType_and_AccessTokenLifetime_are_fixed_in_code()
    {
        // spec.md required case: GenerateOAuthClient(scopes=[…]) → grant type is always client_credentials and
        // the token TTL is always 15 minutes, "neither configurable via input" (BR-17).
        OAuthClientGenerationService.GrantType.Should().Be("client_credentials");
        OAuthClientGenerationService.AccessTokenLifetime.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void Generate_applies_the_selected_scopes_and_returns_the_secret_once()
    {
        var result = CreateService().Generate(
            Command("Core Bus Client", OAuthScopes.SurveyLinksRead, OAuthScopes.ResponsesWrite),
            currentActive: null);

        result.Succeeded.Should().BeTrue();
        var generated = result.Generated!;

        generated.Credential.Mechanism.Should().Be(CredentialMechanism.OAuthClient);
        generated.Credential.LabelOrClientName.Should().Be("Core Bus Client");
        generated.Credential.Scopes.Should().BeEquivalentTo(
            new[] { OAuthScopes.SurveyLinksRead, OAuthScopes.ResponsesWrite });

        generated.Secret.Should().NotBeNullOrWhiteSpace();
        generated.Credential.SecretHash.Should().NotBe(generated.Secret);
        _hasher.Verify(generated.Secret, generated.Credential.SecretHash).Should().BeTrue();
    }

    [Fact]
    public void Generate_uses_the_credential_id_as_the_client_id()
    {
        var generated = CreateService().Generate(Command(), currentActive: null).Generated!;

        generated.ClientId.Should().Be(generated.Credential.Id);
        generated.ClientId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Generate_stamps_the_credential_active_at_the_injected_time()
    {
        var generated = CreateService().Generate(Command(), currentActive: null).Generated!;

        generated.Credential.Status.Should().Be(CredentialStatus.Active);
        generated.Credential.GeneratedAt.Should().Be(Now);
        generated.Credential.GeneratedBy.Should().Be(ActorId);
        generated.Credential.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Generate_normalises_duplicate_scopes_into_catalogue_order()
    {
        var generated = CreateService().Generate(
            Command("Client", OAuthScopes.ResponsesWrite, OAuthScopes.SurveyRequestsWrite, OAuthScopes.ResponsesWrite),
            currentActive: null).Generated!;

        generated.Credential.Scopes.Should().Equal(
            OAuthScopes.SurveyRequestsWrite, OAuthScopes.ResponsesWrite);
    }

    [Fact]
    public void Generate_implicitly_revokes_the_prior_active_credential()
    {
        // BR-16 applies to both mechanisms — a regenerated OAuth client supersedes the previous one.
        var current = ActiveClient();

        var generated = CreateService().Generate(Command("Replacement"), currentActive: current).Generated!;

        generated.Superseded.Should().BeSameAs(current);
        current.Status.Should().Be(CredentialStatus.Revoked);
        current.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void Generate_returns_invalid_client_name_required_when_the_client_name_is_missing()
    {
        // VR-F10 — client name is required, exactly as the API-key label is.
        var result = CreateService().Generate(Command(clientName: ""), currentActive: null);

        result.Succeeded.Should().BeFalse();
        result.Errors.Single().Code.Should().Be(IntegrationErrorCodes.ClientNameRequired);
        result.Errors.Single().Field.Should().Be(IntegrationFields.ClientName);
    }

    [Fact]
    public void Generate_returns_invalid_scopes_required_when_no_scope_was_selected()
    {
        // A scopeless token can call no scenario endpoint at all (BR-17's "scopes limit which endpoints a
        // token may call"), so issuing one silently would provision a credential that can never work.
        var result = CreateService().Generate(
            new CredentialGenerateCommand(
                IntegrationId, CredentialMechanism.OAuthClient, "Client", Array.Empty<string>(), ActorId),
            currentActive: null);

        result.Succeeded.Should().BeFalse();
        result.Errors.Single().Code.Should().Be(IntegrationErrorCodes.ScopesRequired);
    }

    [Fact]
    public void Generate_returns_invalid_unknown_scope_for_a_scope_outside_the_ratified_five()
    {
        var result = CreateService().Generate(Command("Client", "surveys:delete"), currentActive: null);

        result.Succeeded.Should().BeFalse();
        result.Errors.Single().Code.Should().Be(IntegrationErrorCodes.UnknownScope);
        result.Errors.Single().Field.Should().Be(IntegrationFields.Scopes);
    }

    [Fact]
    public void OAuthScopes_catalogue_holds_exactly_the_five_ratified_scopes_one_per_scenario()
    {
        // BR-26 — one scope per scenario endpoint, following ‹resource›:‹verb›.
        OAuthScopes.All.Should().Equal(
            "survey-requests:write",
            "survey-links:read",
            "survey-definitions:read",
            "survey-embed:read",
            "responses:write");

        OAuthScopes.For(Scenario.Dispatch).Should().Be("survey-requests:write");
        OAuthScopes.For(Scenario.RedirectLink).Should().Be("survey-links:read");
        OAuthScopes.For(Scenario.JsonRender).Should().Be("survey-definitions:read");
        OAuthScopes.For(Scenario.IframeEmbed).Should().Be("survey-embed:read");
        OAuthScopes.For(Scenario.ResponseIngestion).Should().Be("responses:write");
    }

    private static Credential ActiveClient() => new()
    {
        Id = Guid.NewGuid(),
        IntegrationId = IntegrationId,
        Mechanism = CredentialMechanism.OAuthClient,
        LabelOrClientName = "Existing client",
        SecretHash = "existing-hash",
        Scopes = new[] { OAuthScopes.SurveyRequestsWrite },
        Status = CredentialStatus.Active,
        GeneratedAt = Now.AddDays(-5),
        GeneratedBy = ActorId,
    };
}

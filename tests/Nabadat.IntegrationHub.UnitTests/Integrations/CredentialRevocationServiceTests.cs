using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.IntegrationHub.Application.Integrations;
using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Integrations;

/// <summary>
/// T072 [US3] — unit tests for <c>CredentialRevocationService</c> (BR-16 / AC-S2-03 / Status Lifecycle):
/// revocation is <b>immediate and one-way</b>, and every subsequent authentication attempt with that
/// credential fails with <c>E-1401</c> carrying the ratified message copy
/// <i>"API key was revoked on ‹date›. Generate a new key in Integrations."</i>
///
/// <para>Contract these tests pin for the implementer (T080):
/// <list type="bullet">
///   <item><c>CredentialRevocationService(TimeProvider)</c> in <c>Application/Integrations/</c> with three pure
///   methods: <c>CredentialRevocationResult Revoke(Credential)</c>, <c>bool IsUsable(Credential?)</c>, and
///   <c>CredentialAuthCheck Authenticate(Credential?)</c>. It mutates the entity in memory; the caller
///   persists — so a standalone revoke (US8) and an implicit supersession (BR-16) stamp the row identically.</item>
///   <item>Re-revoking is <c>credential.already_revoked</c> → 409, and must <b>not</b> rewrite
///   <see cref="Credential.RevokedAt"/> — the audit trail records when the key actually stopped working.</item>
///   <item><c>Authenticate</c> returns the pipeline's internal
///   <see cref="ResultCode.InvalidCredentials"/>; US4's <c>ResultCodeMapper</c> turns that into the wire
///   <c>401 E-1401</c>. Keeping the enum here avoids two spellings of the same outcome.</item>
///   <item>There is <b>no</b> un-revoke operation anywhere on the type — asserted below as API-surface
///   absence, which is how spec.md words it ("no such operation exists").</item>
/// </list></para>
/// </summary>
public sealed class CredentialRevocationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 9, 30, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);

    private CredentialRevocationService CreateService() => new(_time);

    [Fact]
    public void Revoke_marks_the_credential_revoked_at_the_injected_time()
    {
        var key = ActiveKey();

        var result = CreateService().Revoke(key);

        result.Succeeded.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        key.Status.Should().Be(CredentialStatus.Revoked);
        key.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void Authenticate_returns_invalid_credentials_after_the_key_is_revoked()
    {
        // spec.md required case: Revoke(key=K1) → subsequent auth check for K1 → Invalid → 401 E-1401.
        var key = ActiveKey();
        var service = CreateService();

        service.Authenticate(key).IsValid.Should().BeTrue();

        service.Revoke(key);

        var check = service.Authenticate(key);
        check.IsValid.Should().BeFalse();
        check.Failure.Should().Be(ResultCode.InvalidCredentials);
    }

    [Fact]
    public void Authenticate_returns_the_ratified_revocation_message_copy_naming_the_date()
    {
        // AC-S2-03's shipped copy, verbatim apart from the interpolated date.
        var key = ActiveKey();
        var service = CreateService();
        service.Revoke(key);

        service.Authenticate(key).Message.Should()
            .Be("API key was revoked on 2026-08-02. Generate a new key in Integrations.");
    }

    [Fact]
    public void Authenticate_names_the_oauth_client_rather_than_a_key_for_an_oauth_credential()
    {
        // Only the API-key wording is normative (AC-S2-03); the OAuth variant must not tell a caller to
        // "generate a new key" when what they hold is a client.
        var client = ActiveKey();
        client.Mechanism = CredentialMechanism.OAuthClient;
        client.Scopes = new[] { OAuthScopes.SurveyRequestsWrite };
        var service = CreateService();
        service.Revoke(client);

        service.Authenticate(client).Message.Should()
            .Be("OAuth client was revoked on 2026-08-02. Generate new credentials in Integrations.");
    }

    [Fact]
    public void Authenticate_returns_invalid_credentials_when_no_credential_resolved_at_all()
    {
        // An unknown or never-generated key resolves to null upstream and must fail identically — the caller
        // learns nothing about whether the key ever existed.
        var check = CreateService().Authenticate(null);

        check.IsValid.Should().BeFalse();
        check.Failure.Should().Be(ResultCode.InvalidCredentials);
    }

    [Fact]
    public void Revoke_returns_already_revoked_and_preserves_the_original_timestamp_on_a_second_call()
    {
        var key = ActiveKey();
        var service = CreateService();
        service.Revoke(key);

        _time.SetUtcNow(Now.AddHours(6));
        var second = service.Revoke(key);

        second.Succeeded.Should().BeFalse();
        second.Errors.Single().Code.Should().Be(IntegrationErrorCodes.CredentialAlreadyRevoked);
        key.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void IsUsable_is_true_only_while_the_credential_is_active()
    {
        var key = ActiveKey();
        var service = CreateService();

        service.IsUsable(key).Should().BeTrue();
        service.IsUsable(null).Should().BeFalse();

        service.Revoke(key);
        service.IsUsable(key).Should().BeFalse();
    }

    [Fact]
    public void The_service_exposes_no_un_revoke_operation()
    {
        // Status Lifecycle: Active → Revoked is one-way and "there is no un-revoke action anywhere". This is
        // an API-surface assertion, not a runtime rejection — the operation must not exist to be called.
        var methodNames = typeof(CredentialRevocationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToList();

        methodNames.Should().NotContain(name =>
            name.Contains("Unrevoke", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Restore", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Reactivate", StringComparison.OrdinalIgnoreCase));
    }

    private static Credential ActiveKey() => new()
    {
        Id = Guid.NewGuid(),
        IntegrationId = Guid.NewGuid(),
        Mechanism = CredentialMechanism.ApiKey,
        LabelOrClientName = "Core Bus Key",
        SecretHash = "hash",
        Status = CredentialStatus.Active,
        GeneratedAt = Now.AddDays(-1),
        GeneratedBy = Guid.NewGuid(),
    };
}

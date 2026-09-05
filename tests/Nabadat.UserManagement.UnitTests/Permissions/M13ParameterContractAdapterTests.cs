using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Npgsql;
using Xunit;
using Nabadat.UserManagement.Application.Permissions.Interfaces;

namespace Nabadat.UserManagement.UnitTests.Permissions;

/// <summary>
/// T099 — write-first unit tests for <c>M13ParameterContractAdapter</c> (T104, US3).
/// M-13 (or any external scope provider) pushes parameter <i>definitions</i> — names
/// and their full allowed-value sets — which M-10 validates and persists into
/// <c>data_scope_parameter_definitions</c>. The adapter is provider-agnostic: it
/// stores whatever names/values the payload carries with no hardcoded provider
/// branching, and rejects payloads that would corrupt the scope model — a reserved
/// system name, an empty value set, or a payload over the 500-definition ceiling
/// (permissions-api.md).
///
/// These production types do not exist yet, so this project fails to COMPILE — the
/// valid red state for a write-first story (CLAUDE.md Unit Test Policy, rule 7).
/// </summary>
public sealed class M13ParameterContractAdapterTests
{
    private readonly IDataScopeService _scopes = Substitute.For<IDataScopeService>();
    private readonly FakeTimeProvider _clock = new();

    private M13ParameterContractAdapter CreateSut() => new(_scopes, new FakeTenantDbContext(), _clock);

    private static M13ParameterPayload Payload(params M13ParameterDefinition[] parameters) => new()
    {
        SourceModule = "M-13",
        Parameters = parameters,
    };

    private static M13ParameterDefinition Param(string name, params string[] values) => new()
    {
        Name = name,
        Label = name,
        AllowedValues = values,
    };

    // ── Case 1: a valid payload persists the parameter names and their allowed values ──
    [Fact]
    public async Task StoreParameterDefinitions_persists_names_and_allowed_values()
    {
        await CreateSut().StoreParameterDefinitionsAsync(
            Payload(Param("branch", "Riyadh", "Jeddah", "Dammam")));

        await _scopes.Received().StoreParameterDefinitionsAsync(
            Arg.Is<IReadOnlyList<DataScopeParameterDefinition>>(d =>
                d.Any(p => p.ParameterName == "branch"
                           && p.AllowedValues.Contains("Riyadh")
                           && p.AllowedValues.Contains("Jeddah")
                           && p.AllowedValues.Contains("Dammam"))),
            Arg.Any<CancellationToken>());
    }

    // ── Case 2: a reserved system parameter name is rejected ──
    [Fact]
    public async Task StoreParameterDefinitions_throws_validation_when_parameter_name_is_reserved()
    {
        // "user_id" collides with a system identity field — it must not become a scope parameter.
        var act = () => CreateSut().StoreParameterDefinitionsAsync(
            Payload(Param("user_id", "alice", "bob")));

        await act.Should().ThrowAsync<ValidationException>();
        await _scopes.DidNotReceive().StoreParameterDefinitionsAsync(
            Arg.Any<IReadOnlyList<DataScopeParameterDefinition>>(),
            Arg.Any<CancellationToken>());
    }

    // ── Case 3: a definition with an empty allowed-value set is rejected ──
    [Fact]
    public async Task StoreParameterDefinitions_throws_validation_when_allowed_values_empty()
    {
        var act = () => CreateSut().StoreParameterDefinitionsAsync(
            Payload(Param("branch")));  // no values

        await act.Should().ThrowAsync<ValidationException>();
        await _scopes.DidNotReceive().StoreParameterDefinitionsAsync(
            Arg.Any<IReadOnlyList<DataScopeParameterDefinition>>(),
            Arg.Any<CancellationToken>());
    }

    // ── Case 4: a payload over the 500-definition ceiling is rejected ──
    [Fact]
    public async Task StoreParameterDefinitions_throws_validation_when_exceeding_500_definitions()
    {
        var tooMany = Enumerable.Range(0, 501)
            .Select(i => Param($"param_{i}", "value"))
            .ToArray();

        var act = () => CreateSut().StoreParameterDefinitionsAsync(Payload(tooMany));

        await act.Should().ThrowAsync<ValidationException>();
        await _scopes.DidNotReceive().StoreParameterDefinitionsAsync(
            Arg.Any<IReadOnlyList<DataScopeParameterDefinition>>(),
            Arg.Any<CancellationToken>());
    }
}

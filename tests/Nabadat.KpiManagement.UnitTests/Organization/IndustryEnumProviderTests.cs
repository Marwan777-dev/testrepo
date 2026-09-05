using FluentAssertions;
using Nabadat.KpiManagement.Application.Organization;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Organization;

/// <summary>
/// T131 [US6] — unit tests for <c>IndustryEnumProvider</c> (the canonical industry list, FR-050 / R13),
/// covering the spec.md US-6 Required cases.
/// <para>
/// (2026-06-24 re-home): the provider is now M-06-owned and the single source of truth, so the old
/// "returns the same set as the M-11 industry enum" cross-check is dropped — there is no separate
/// M-11 enum to compare against.
/// </para>
/// <para>
/// Contract pinned for the implementer (T144 / T145):
/// <list type="bullet">
///   <item><c>Industry</c> enum in <c>Domain/ValueObjects/</c> with the canonical six members in order:
///   Banking, Telecommunications, Government, Automotive, Entertainment, Services.</item>
///   <item><c>IndustryEnumProvider : IIndustryEnumProvider</c> in <c>Application/Organization/</c>
///   exposing <c>IReadOnlyList&lt;Industry&gt; GetAll()</c> (canonical order) and
///   <c>bool IsValid(string? industry)</c> (accepts each canonical name, rejects unknowns).</item>
/// </list>
/// </para>
/// </summary>
public sealed class IndustryEnumProviderTests
{
    private static readonly IndustryEnumProvider Provider = new();

    [Fact]
    public void GetAll_returns_the_six_canonical_industries_in_order()
    {
        Provider.GetAll().Should().Equal(
            Industry.Banking,
            Industry.Telecommunications,
            Industry.Government,
            Industry.Automotive,
            Industry.Entertainment,
            Industry.Services);
    }

    [Theory]
    [InlineData("Banking")]
    [InlineData("Telecommunications")]
    [InlineData("Government")]
    [InlineData("Automotive")]
    [InlineData("Entertainment")]
    [InlineData("Services")]
    public void IsValid_accepts_each_canonical_industry_name(string industry)
    {
        Provider.IsValid(industry).Should().BeTrue();
    }

    [Theory]
    [InlineData("Aerospace")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_rejects_unknown_or_missing_industry(string? industry)
    {
        Provider.IsValid(industry).Should().BeFalse();
    }
}

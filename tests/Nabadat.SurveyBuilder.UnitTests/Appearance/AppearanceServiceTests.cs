using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Appearance;
using Nabadat.SurveyBuilder.Application.Appearance.Dtos;
using Nabadat.SurveyBuilder.Application.Appearance.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Appearance;

/// <summary>
/// T050 [US1] — unit tests for <c>AppearanceService</c> (F4): in Inherited mode every token resolves
/// from the tenant design guidelines (M-11); Customize mode unlocks the survey's own theme; and a
/// save with <c>Background = Image</c> requires a file handle.
/// <para>
/// Contract pinned for the implementer (T080):
/// <list type="bullet">
///   <item><c>AppearanceService</c> lives in <c>Application/Appearance/</c>; ctor
///   <c>(IThemeStore themes, ITenantDesignGuidelinesReader guidelines, ITenantDbContext context,
///   TimeProvider timeProvider)</c>.</item>
///   <item><c>Task&lt;ResolvedAppearance&gt; ResolveAsync(Guid surveyId, CancellationToken ct = default)</c>
///   returns the effective tokens — sourced from <c>ITenantDesignGuidelinesReader</c> when the survey
///   theme mode is <see cref="ThemeMode.Inherited"/>, otherwise from the stored <c>Theme</c>.</item>
///   <item><c>Task&lt;AppearanceSaveResult&gt; SaveAsync(SaveThemeCommand command, CancellationToken ct = default)</c>
///   where <c>SaveThemeCommand</c> carries <c>Guid SurveyId</c>, <c>ThemeMode Mode</c>,
///   <c>BackgroundType BackgroundType</c>, <c>string? BackgroundImageHandle</c>, <c>string? PrimaryColour</c>, …;
///   <c>AppearanceSaveResult(bool IsValid, IReadOnlyList&lt;string&gt; Errors)</c>. Saving with
///   <c>BackgroundType.Image</c> and a null handle fails with <c>theme.background_image.required</c>.</item>
///   <item>New Domain value objects (T056/T062): <c>ThemeMode { Inherited, Customized }</c>,
///   <c>BackgroundType { Solid, Gradient, Image, Pattern }</c>.</item>
///   <item><c>ITenantDesignGuidelinesReader</c> (M-11 port, in <c>Domain/Interfaces/</c>):
///   <c>Task&lt;TenantDesignGuidelines&gt; GetDesignGuidelinesAsync(CancellationToken ct = default)</c>
///   returning a token set including <c>string PrimaryColour</c>.</item>
///   <item><c>IThemeStore</c> (in <c>Application/Appearance/Interfaces/</c>) exposes at least
///   <c>Task&lt;Theme?&gt; GetBySurveyAsync(Guid, CancellationToken)</c> and
///   <c>Task&lt;ThemeMode&gt; GetModeAsync(Guid, CancellationToken)</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AppearanceServiceTests
{
    private readonly IThemeStore _themes = Substitute.For<IThemeStore>();
    private readonly ITenantDesignGuidelinesReader _guidelines = Substitute.For<ITenantDesignGuidelinesReader>();
    private readonly RecordingTenantDbContext _context = new();

    private AppearanceService CreateService() =>
        new(_themes, _guidelines, _context, TestTime.Provider());

    [Fact]
    public async Task ResolveAsync_resolves_tokens_from_the_tenant_guidelines_in_inherited_mode()
    {
        var surveyId = Guid.NewGuid();
        _themes.GetModeAsync(surveyId, Arg.Any<CancellationToken>()).Returns(ThemeMode.Inherited);
        _guidelines.GetDesignGuidelinesAsync(Arg.Any<CancellationToken>())
            .Returns(new TenantDesignGuidelines(PrimaryColour: "#0D8BBC"));

        var resolved = await CreateService().ResolveAsync(surveyId);

        resolved.PrimaryColour.Should().Be("#0D8BBC");
        await _guidelines.Received(1).GetDesignGuidelinesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_rejects_an_image_background_without_a_file_handle()
    {
        var command = new SaveThemeCommand(
            SurveyId: Guid.NewGuid(),
            Mode: ThemeMode.Customized,
            BackgroundType: BackgroundType.Image,
            BackgroundImageHandle: null,
            PrimaryColour: "#0D8BBC");

        var result = await CreateService().SaveAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("theme.background_image.required");
    }

    [Fact]
    public async Task SaveAsync_accepts_a_solid_background_in_customize_mode()
    {
        var command = new SaveThemeCommand(
            SurveyId: Guid.NewGuid(),
            Mode: ThemeMode.Customized,
            BackgroundType: BackgroundType.Solid,
            BackgroundImageHandle: null,
            PrimaryColour: "#0D8BBC");

        var result = await CreateService().SaveAsync(command);

        result.IsValid.Should().BeTrue();
    }
}

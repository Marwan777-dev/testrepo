using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Translations;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Translations;

/// <summary>
/// T206 [US6] — unit tests for <c>LocaleFallbackPolicy</c> (BR-3.2). A survey may ship before its
/// Arabic bundle is complete, so at render time any key missing from the target locale resolves to
/// the English source value ("translations may be completed later").
/// <para>
/// Contract pinned for the implementer (T212 — "matching T206"):
/// <list type="bullet">
///   <item><c>LocaleFallbackPolicy</c> lives in <c>Application/Translations/</c>; it is stateless
///   (no ctor dependencies).</item>
///   <item><c>string? Resolve(IReadOnlyDictionary&lt;string, TranslationBundle&gt; bundlesByLocale,
///   string locale, string key)</c> resolves one key for <paramref name="locale"/>:
///     <list type="number">
///       <item>the target locale's value when present and non-empty;</item>
///       <item>otherwise the English source value (<c>LocaleFallbackPolicy.SourceLocale == "en"</c>);</item>
///       <item>otherwise <c>null</c> when neither locale carries the key.</item>
///     </list></item>
///   <item>An empty/whitespace target value counts as "not translated" and falls back to English —
///   a saved-but-blank Arabic string must not blank out the rendered survey.</item>
/// </list>
/// </para>
/// </summary>
public sealed class LocaleFallbackPolicyTests
{
    private const string EnglishWelcome = "Welcome to our survey";
    private const string ArabicWelcome = "مرحبًا بكم في استبياننا";

    private static TranslationBundle Bundle(string locale, params (string Key, string Value)[] entries) =>
        new(locale, entries.ToDictionary(e => e.Key, e => e.Value));

    private static IReadOnlyDictionary<string, TranslationBundle> Bundles(params TranslationBundle[] bundles) =>
        bundles.ToDictionary(b => b.Locale);

    private static string? Resolve(IReadOnlyDictionary<string, TranslationBundle> bundles, string locale, string key) =>
        new LocaleFallbackPolicy().Resolve(bundles, locale, key);

    [Fact]
    public void Resolve_returns_the_target_value_when_the_target_locale_has_the_key()
    {
        var bundles = Bundles(
            Bundle("en", ("survey.welcome", EnglishWelcome)),
            Bundle("ar", ("survey.welcome", ArabicWelcome)));

        Resolve(bundles, "ar", "survey.welcome").Should().Be(ArabicWelcome);
    }

    [Fact]
    public void Resolve_falls_back_to_english_when_the_arabic_key_is_missing()
    {
        // The required case: Arabic bundle exists but does not carry `survey.welcome` yet (BR-3.2).
        var bundles = Bundles(
            Bundle("en", ("survey.welcome", EnglishWelcome)),
            Bundle("ar", ("survey.name", "استبيان ما بعد الزيارة")));

        Resolve(bundles, "ar", "survey.welcome").Should().Be(EnglishWelcome);
    }

    [Fact]
    public void Resolve_falls_back_to_english_when_the_target_locale_bundle_is_absent()
    {
        // No Arabic row saved at all — every key resolves to English.
        var bundles = Bundles(Bundle("en", ("survey.welcome", EnglishWelcome)));

        Resolve(bundles, "ar", "survey.welcome").Should().Be(EnglishWelcome);
    }

    [Fact]
    public void Resolve_falls_back_to_english_when_the_target_value_is_blank()
    {
        var bundles = Bundles(
            Bundle("en", ("survey.welcome", EnglishWelcome)),
            Bundle("ar", ("survey.welcome", "   ")));

        Resolve(bundles, "ar", "survey.welcome").Should().Be(EnglishWelcome);
    }

    [Fact]
    public void Resolve_returns_null_when_neither_locale_carries_the_key()
    {
        var bundles = Bundles(
            Bundle("en", ("survey.name", "Post-visit survey")),
            Bundle("ar", ("survey.name", "استبيان ما بعد الزيارة")));

        Resolve(bundles, "ar", "survey.welcome").Should().BeNull();
    }

    [Fact]
    public void SourceLocale_is_english()
    {
        LocaleFallbackPolicy.SourceLocale.Should().Be("en");
    }
}

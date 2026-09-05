using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Translations;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Translations;

/// <summary>
/// T204 [US6] — unit tests for <c>TranslationBundleBuilder</c> (F11). Given the English source bundle
/// (from <see cref="TranslatableStringExtractor"/>) and a target-locale bundle, the builder produces
/// the <b>resolved view</b> the GET endpoint returns: every source key resolved to its target value
/// or English fallback, plus the list of keys still missing from the target
/// (contracts/translations.md § GET /translations/{locale} — <c>keys</c> + <c>missing_keys</c>).
/// <para>
/// Contract pinned for the implementer:
/// <list type="bullet">
///   <item><c>TranslationBundleBuilder</c> lives in <c>Application/Translations/</c>; ctor
///   <c>(LocaleFallbackPolicy fallbackPolicy)</c> — the builder applies the T206 policy across every
///   source key (policy = resolve-one-key primitive; builder = resolve-the-whole-bundle).</item>
///   <item><c>ResolvedTranslationBundle Build(TranslationBundle source, TranslationBundle target)</c>.
///   <c>source</c> is the English bundle; <c>target</c> is the requested locale's bundle (its
///   <c>Keys</c> may be empty when no row is saved yet, but its <c>Locale</c> is the requested tag).</item>
///   <item><c>ResolvedTranslationBundle</c> (record in <c>Application/Translations/</c>):
///   <c>(string Locale, IReadOnlyDictionary&lt;string,string&gt; Keys, IReadOnlyList&lt;string&gt; MissingKeys)</c>.
///   <c>Locale</c> is the target locale; <c>Keys</c> is driven by the <b>source</b> key set (every
///   source key present, resolved); <c>MissingKeys</c> lists the source keys with no non-empty target
///   value. Target keys not present in the source (stale, e.g. a deleted question) are dropped.</item>
/// </list>
/// </para>
/// <para><b>GAP (TODO-M01-005-adjacent):</b> tasks.md T208–T217 create the extractor (T211), the
/// fallback policy (T212), and <c>TranslationBundleService</c> (T213) but assign <b>no task</b> to
/// create <c>TranslationBundleBuilder</c>, even though it is a named unit-under-test here. Tracked as
/// a GAP in TODO.md so the missing implementation task is triaged, not silently forgotten.</para>
/// </summary>
public sealed class TranslationBundleBuilderTests
{
    private const string EnName = "Post-visit survey";
    private const string EnWelcome = "Welcome to our survey";
    private const string EnThanks = "Thank you for your time";
    private const string ArName = "استبيان ما بعد الزيارة";
    private const string ArWelcome = "مرحبًا بكم في استبياننا";

    private static TranslationBundle Bundle(string locale, params (string Key, string Value)[] entries) =>
        new(locale, entries.ToDictionary(e => e.Key, e => e.Value));

    private static ResolvedTranslationBundle Build(TranslationBundle source, TranslationBundle target) =>
        new TranslationBundleBuilder(new LocaleFallbackPolicy()).Build(source, target);

    private static TranslationBundle EnglishSource() => Bundle(
        "en",
        ("survey.name", EnName),
        ("survey.welcome", EnWelcome),
        ("survey.thanks", EnThanks));

    [Fact]
    public void Build_stamps_the_resolved_bundle_with_the_target_locale()
    {
        var resolved = Build(EnglishSource(), Bundle("ar"));

        resolved.Locale.Should().Be("ar");
    }

    [Fact]
    public void Build_uses_the_target_value_where_present_and_english_fallback_elsewhere()
    {
        var target = Bundle("ar", ("survey.name", ArName), ("survey.welcome", ArWelcome));

        var resolved = Build(EnglishSource(), target);

        resolved.Keys.Should().Contain("survey.name", ArName);       // translated
        resolved.Keys.Should().Contain("survey.welcome", ArWelcome); // translated
        resolved.Keys.Should().Contain("survey.thanks", EnThanks);   // fallback to English (BR-3.2)
    }

    [Fact]
    public void Build_lists_exactly_the_source_keys_missing_from_the_target()
    {
        var target = Bundle("ar", ("survey.name", ArName), ("survey.welcome", ArWelcome));

        var resolved = Build(EnglishSource(), target);

        resolved.MissingKeys.Should().BeEquivalentTo(new[] { "survey.thanks" });
    }

    [Fact]
    public void Build_reports_no_missing_keys_when_the_target_fully_covers_the_source()
    {
        var target = Bundle(
            "ar",
            ("survey.name", ArName),
            ("survey.welcome", ArWelcome),
            ("survey.thanks", "شكرًا لوقتكم"));

        var resolved = Build(EnglishSource(), target);

        resolved.MissingKeys.Should().BeEmpty();
        resolved.Keys.Values.Should().NotContain(EnName).And.NotContain(EnWelcome).And.NotContain(EnThanks);
    }

    [Fact]
    public void Build_reports_all_source_keys_missing_when_the_target_is_empty()
    {
        var resolved = Build(EnglishSource(), Bundle("ar"));

        resolved.MissingKeys.Should().BeEquivalentTo(new[] { "survey.name", "survey.welcome", "survey.thanks" });
        // With an empty target, every resolved value is the English fallback.
        resolved.Keys.Should().Contain("survey.name", EnName);
        resolved.Keys.Should().Contain("survey.welcome", EnWelcome);
        resolved.Keys.Should().Contain("survey.thanks", EnThanks);
    }

    [Fact]
    public void Build_drops_stale_target_keys_that_are_not_in_the_source()
    {
        // e.g. a question was deleted after the Arabic bundle was saved — its key must not surface.
        var target = Bundle(
            "ar",
            ("survey.name", ArName),
            ("question.deleted.text", "نص محذوف"));

        var resolved = Build(EnglishSource(), target);

        resolved.Keys.Keys.Should().BeEquivalentTo(new[] { "survey.name", "survey.welcome", "survey.thanks" });
        resolved.Keys.Should().NotContainKey("question.deleted.text");
    }
}

using FluentAssertions;
using Nabadat.IntegrationHub.Application.Parameters;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Parameters;

/// <summary>
/// T043 [US2] — unit tests for <c>ApiFieldNameSuggester</c>: the SCR-06 auto-suggest that derives the
/// <c>snake_case</c> API field name from the EN parameter name as the user types (FR-S6-02, AC-S6-02).
///
/// <para>Contract these tests pin for the implementer (T051):
/// <list type="bullet">
///   <item><c>ApiFieldNameSuggester</c> in <c>Application/Parameters/</c> with
///   <c>string Suggest(string? nameEn)</c> — pure, no database access, safe to call on every keystroke.</item>
///   <item>The three normative transformation steps, in this order: <b>lowercase</b> → <b>whitespace becomes
///   <c>_</c></b> → <b>every remaining invalid character is stripped</b> (spec.md's auto-suggest rule; there is
///   no transliteration, deliberately — see the accented-input case below).</item>
///   <item>The output is always a <b>legal</b> API field name — it satisfies the baseline's
///   <c>ck_parameters_api_field_format</c> CHECK (<c>^[a-z][a-z0-9_]*$</c>) or is empty. A suggestion that the
///   database would reject is a defect: the user would be shown a value they cannot save.</item>
///   <item>The suggestion is only ever a <b>starting point</b> — it stays manually editable until BR-11's lock
///   (<c>ApiFieldNameLockGuard</c>, T045/T053), so the suggester never validates uniqueness.</item>
/// </list></para>
/// </summary>
public sealed class ApiFieldNameSuggesterTests
{
    private static readonly ApiFieldNameSuggester Suggester = new();

    /// <summary>The baseline CHECK the suggestion must always satisfy.</summary>
    private const string ApiFieldPattern = "^[a-z][a-z0-9_]*$";

    [Fact]
    public void Suggest_returns_wait_time_for_the_normative_example()
    {
        // The normative spec.md required case: Suggest("Wait Time") → "wait_time".
        Suggester.Suggest("Wait Time").Should().Be("wait_time");
    }

    [Fact]
    public void Suggest_lowercases_the_english_name()
    {
        Suggester.Suggest("BRANCH").Should().Be("branch");
    }

    [Fact]
    public void Suggest_replaces_each_whitespace_run_with_a_single_underscore()
    {
        // A double space (or a tab) must not become "__" — that reads as a typo in the suggested key.
        Suggester.Suggest("Average  Handling\tTime").Should().Be("average_handling_time");
    }

    [Fact]
    public void Suggest_strips_invalid_characters_without_transliterating_them()
    {
        // The normative spec.md required case: Suggest("Été & Café!") — non-[a-z0-9\s] characters are STRIPPED
        // (no transliteration: "é" does not become "e"), and the result is still a legal snake_case candidate
        // the user may edit by hand before first use.
        var suggestion = Suggester.Suggest("Été & Café!");

        suggestion.Should().Be("t_caf");
        suggestion.Should().MatchRegex(ApiFieldPattern);
    }

    [Fact]
    public void Suggest_keeps_digits_that_are_not_leading()
    {
        Suggester.Suggest("Queue 2 Wait").Should().Be("queue_2_wait");
    }

    [Fact]
    public void Suggest_drops_leading_digits_so_the_field_starts_with_a_letter()
    {
        // "^[a-z]..." — a suggestion starting with a digit would be rejected by the column CHECK.
        Suggester.Suggest("2nd Visit").Should().Be("nd_visit");
        Suggester.Suggest("2nd Visit").Should().MatchRegex(ApiFieldPattern);
    }

    [Fact]
    public void Suggest_trims_leading_and_trailing_underscores()
    {
        Suggester.Suggest("  Wait Time  ").Should().Be("wait_time");
        Suggester.Suggest("- Wait Time -").Should().Be("wait_time");
    }

    [Fact]
    public void Suggest_preserves_an_already_snake_case_name()
    {
        Suggester.Suggest("wait_time").Should().Be("wait_time");
    }

    [Fact]
    public void Suggest_returns_empty_when_nothing_usable_remains()
    {
        // An all-Arabic name yields no [a-z0-9] characters at all. Returning empty (rather than a fabricated
        // key) is what lets SCR-06 leave the field blank for the user to type, instead of suggesting garbage.
        Suggester.Suggest("وقت الانتظار").Should().BeEmpty();
        Suggester.Suggest("!!!").Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Suggest_returns_empty_for_a_missing_english_name(string? nameEn)
    {
        // Called on every keystroke, including the first — an empty box must not throw.
        Suggester.Suggest(nameEn).Should().BeEmpty();
    }
}

using System.Text;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T051 — the SCR-06 API-field auto-suggest (FR-S6-02, AC-S6-02): derives a <c>snake_case</c> candidate from the
/// EN parameter name as the user types. Pure and allocation-cheap, because it runs on every keystroke.
///
/// <para>The three normative transformation steps, in order: <b>lowercase</b> → <b>whitespace becomes
/// <c>_</c></b> → <b>strip every remaining invalid character</b>. There is deliberately <b>no
/// transliteration</b>: "Été" yields "t", not "ete". That is the SRS's ratified rule, and the reason the field
/// stays manually editable until BR-11's lock — a name in a non-Latin script simply gets an empty suggestion and
/// the user types the key themselves.</para>
///
/// <para>Whatever comes out is guaranteed to satisfy the baseline's <c>ck_parameters_api_field_format</c> CHECK
/// (<c>^[a-z][a-z0-9_]*$</c>) or be empty — never a value the database would reject. That is why the leading
/// digits are dropped and the underscores collapsed: suggesting <c>2nd_visit</c> or <c>t__caf</c> would show the
/// user a key they cannot save.</para>
///
/// <para>Uniqueness is <b>not</b> this type's concern — <see cref="ApiFieldNameUniquenessValidator"/> owns
/// VR-F06, and a suggestion that collides is corrected by the user (or rejected on save).</para>
/// </summary>
public sealed class ApiFieldNameSuggester
{
    /// <summary>
    /// Suggests an API field name for <paramref name="nameEn"/>, or an empty string when nothing usable remains
    /// (a missing name, or one with no ASCII letters/digits at all).
    /// </summary>
    public string Suggest(string? nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameEn))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(nameEn.Length);

        foreach (var character in nameEn)
        {
            if (char.IsWhiteSpace(character))
            {
                // A whitespace run collapses to one underscore, so "Average  Handling" is not "average__handling".
                Append(builder, '_');
                continue;
            }

            var lowered = char.ToLowerInvariant(character);

            if (lowered is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(lowered);
            }
            else if (lowered == '_')
            {
                Append(builder, '_');
            }

            // Everything else — accents, punctuation, non-Latin scripts — is stripped, not transliterated.
        }

        // The CHECK requires the first character to be a letter, so leading underscores and digits go.
        var start = 0;
        while (start < builder.Length && !(builder[start] is >= 'a' and <= 'z'))
        {
            start++;
        }

        var end = builder.Length;
        while (end > start && builder[end - 1] == '_')
        {
            end--;
        }

        return start >= end ? string.Empty : builder.ToString(start, end - start);
    }

    /// <summary>Appends <paramref name="separator"/> unless the previous character already is one.</summary>
    private static void Append(StringBuilder builder, char separator)
    {
        if (builder.Length > 0 && builder[^1] != separator)
        {
            builder.Append(separator);
        }
    }
}

using System.Security.Cryptography;
using System.Text;

namespace Nabadat.SurveyBuilder.Api.Accessors;

/// <summary>
/// Turns the opaque <c>respondent_id</c> render-plan query/body value into the deterministic
/// <c>Guid</c> seed the render service uses for per-respondent Random sampling (research.md §7). A
/// value that is already a GUID is used verbatim; any other string is hashed to a stable GUID so the
/// same respondent always yields the same sample. Empty ⇒ <see cref="Guid.Empty"/>. The hash is a
/// non-cryptographic identity mapping — MD5 is used only for its fixed 16-byte width, not for security.
/// </summary>
public static class RespondentSeed
{
    public static Guid From(string? respondentId)
    {
        if (string.IsNullOrWhiteSpace(respondentId))
        {
            return Guid.Empty;
        }

        if (Guid.TryParse(respondentId, out var parsed))
        {
            return parsed;
        }

        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(respondentId)));
    }
}

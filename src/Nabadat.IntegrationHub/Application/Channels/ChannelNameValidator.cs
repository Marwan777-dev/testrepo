namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// T033 — enforces the bilingual channel-name rules (BR-06): the EN name is required, ≤
/// <see cref="MaxNameLength"/> characters, and unique per tenant <b>case-insensitively</b> (VR-F02); the AR
/// name is required (VR-F03) and bounded by the same column CHECK. Renaming either never touches the
/// channel ID.
///
/// <para>Failures <b>accumulate</b>, so a submission wrong in several fields reports every error at once and
/// SCR-04 renders all of them in one pass instead of one per save round-trip.</para>
///
/// <para>Uniqueness applies to the EN name only — VR-F02 scopes it there, and AR names are not required to
/// be unique. The database backs the EN rule with the functional
/// <c>service_channels_name_en_lower_uniq</c> index.</para>
/// </summary>
public sealed class ChannelNameValidator
{
    /// <summary>
    /// VR-F02's 50-character cap, also applied to the AR name to match the baseline's
    /// <c>ck_service_channels_name_en_length</c> / <c>..._name_ar_length</c> CHECKs — so a name this
    /// validator accepts can never be rejected by the database instead.
    /// </summary>
    public const int MaxNameLength = 50;

    /// <summary>
    /// Validates both names. <paramref name="existingNamesEn"/> is the tenant's other channels' EN names
    /// (already excluding the row being edited); <c>null</c> means there is nothing to collide with.
    /// </summary>
    public ChannelValidationResult Validate(
        string? nameEn,
        string? nameAr,
        IEnumerable<string>? existingNamesEn = null)
    {
        var errors = new List<ChannelValidationError>();

        if (string.IsNullOrWhiteSpace(nameEn))
        {
            errors.Add(new ChannelValidationError(
                ChannelErrorCodes.NameEnRequired, "Channel name · EN is required", ChannelFields.NameEn));
        }
        else
        {
            if (nameEn.Length > MaxNameLength)
            {
                errors.Add(new ChannelValidationError(
                    ChannelErrorCodes.NameEnTooLong,
                    $"Channel name · EN must be {MaxNameLength} characters or fewer",
                    ChannelFields.NameEn));
            }

            if (existingNamesEn is not null
                && existingNamesEn.Any(existing => string.Equals(existing, nameEn, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new ChannelValidationError(
                    ChannelErrorCodes.DuplicateName,
                    "A channel with this name already exists",
                    ChannelFields.NameEn));
            }
        }

        if (string.IsNullOrWhiteSpace(nameAr))
        {
            errors.Add(new ChannelValidationError(
                ChannelErrorCodes.NameArRequired, "Channel name · AR is required", ChannelFields.NameAr));
        }
        else if (nameAr.Length > MaxNameLength)
        {
            errors.Add(new ChannelValidationError(
                ChannelErrorCodes.NameArTooLong,
                $"Channel name · AR must be {MaxNameLength} characters or fewer",
                ChannelFields.NameAr));
        }

        return ChannelValidationResult.From(errors);
    }
}

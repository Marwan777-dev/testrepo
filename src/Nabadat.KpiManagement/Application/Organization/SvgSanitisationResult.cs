namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// The detailed result of <see cref="SvgSanitiser.SanitiseDetailed(byte[])"/>: the sanitised
/// <see cref="Bytes"/> (the byte stream that gets persisted, never the upload bytes) and
/// <see cref="WasModified"/> — true when the sanitiser stripped at least one node or attribute,
/// which the API surfaces as <c>was_sanitised: true</c> so the frontend can show the notice.
/// </summary>
public sealed record SvgSanitisationResult(byte[] Bytes, bool WasModified);

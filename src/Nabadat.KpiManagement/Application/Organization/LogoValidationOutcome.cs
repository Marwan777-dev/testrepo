namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// The three outcomes of <see cref="LogoUploadValidator"/> (FR-050). <see cref="Warning"/> is
/// non-blocking — the upload proceeds (e.g. a file over the recommended size) — whereas
/// <see cref="Invalid"/> blocks it (unsupported content type, zero bytes).
/// </summary>
public enum LogoValidationOutcome
{
    Valid,
    Warning,
    Invalid,
}

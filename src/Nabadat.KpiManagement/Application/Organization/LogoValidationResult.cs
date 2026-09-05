namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// The result of <see cref="LogoUploadValidator.Validate(string, long)"/>: an
/// <see cref="LogoValidationOutcome"/> plus the dotted application <see cref="Code"/> (null when
/// <see cref="LogoValidationOutcome.Valid"/>). The controller maps the code to the API-05 envelope
/// code (e.g. <c>logo.content_type.unsupported</c> → <c>LOGO_CONTENT_TYPE_UNSUPPORTED</c>).
/// </summary>
public sealed record LogoValidationResult(LogoValidationOutcome Outcome, string? Code);

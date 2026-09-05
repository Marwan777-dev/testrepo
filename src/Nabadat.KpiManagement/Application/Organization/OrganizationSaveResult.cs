using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// The outcome of <see cref="OrganizationSaveService.SaveSettingsAsync"/>: on success the resulting
/// <see cref="Settings"/> row; on failure the dotted application <see cref="ErrorCode"/> the
/// controller maps to the API-05 envelope code.
/// </summary>
public sealed record OrganizationSaveResult(bool Succeeded, string? ErrorCode, OrganizationSettings? Settings);

namespace Nabadat.KpiManagement.Application.Organization.Dtos;

/// <summary>
/// The editable Organization fields submitted on <c>PUT /api/v1/tenant/organization</c> (FR-050).
/// Logo is uploaded separately (<c>POST …/logo</c>), so it is not part of this command. Both fields
/// are nullable on the wire so the validator can surface "required" rather than a binding failure.
/// </summary>
public sealed record OrganizationSettingsUpdate(string? Name, string? Industry);

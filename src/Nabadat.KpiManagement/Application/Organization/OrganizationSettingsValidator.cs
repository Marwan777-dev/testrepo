using FluentValidation;
using Nabadat.KpiManagement.Application.Organization.Dtos;
using Nabadat.KpiManagement.Application.Organization.Interfaces;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// Validates the editable Organization fields (FR-050): <see cref="OrganizationSettingsUpdate.Name"/>
/// is required and ≤ 150 chars; <see cref="OrganizationSettingsUpdate.Industry"/> must be one of the
/// canonical six (per the injected <see cref="IIndustryEnumProvider"/> — the single source of truth).
/// Error codes are the dotted application codes the controller maps to the API-05 envelope codes
/// (<c>ORGANIZATION_NAME_REQUIRED</c> / <c>…_TOO_LONG</c> / <c>ORGANIZATION_INDUSTRY_UNKNOWN</c>).
/// </summary>
public sealed class OrganizationSettingsValidator : AbstractValidator<OrganizationSettingsUpdate>
{
    public const string NameRequiredCode = "organization.name.required";
    public const string NameTooLongCode = "organization.name.too_long";
    public const string IndustryUnknownCode = "organization.industry.unknown";

    public const int MaxNameLength = 150;

    public OrganizationSettingsValidator(IIndustryEnumProvider industryProvider)
    {
        RuleFor(o => o.Name)
            .NotEmpty()
            .WithErrorCode(NameRequiredCode)
            .WithMessage("Organization name is required.");

        // Length runs only when a name is present (the required rule already covers empty/whitespace).
        RuleFor(o => o.Name)
            .MaximumLength(MaxNameLength)
            .WithErrorCode(NameTooLongCode)
            .WithMessage($"Organization name must be {MaxNameLength} characters or fewer.")
            .When(o => !string.IsNullOrEmpty(o.Name));

        RuleFor(o => o.Industry)
            .Must(industryProvider.IsValid)
            .WithErrorCode(IndustryUnknownCode)
            .WithMessage("Industry must be one of the supported values.");
    }
}

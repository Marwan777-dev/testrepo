using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// Answers "may this template be edited?" (T192, FR-7.1). A <see cref="TemplateClass.BuiltIn"/>
/// template is platform-curated and <b>locked</b> — no persona may edit it (the P-01 case in the
/// unit test). A <see cref="TemplateClass.Customized"/> template is editable; persona-level
/// authorization to reach the edit endpoint is enforced separately at the API layer via the
/// <c>template.write</c> grant (contracts/templates.md), so this class invariant is
/// actor-independent.
/// </summary>
public sealed class TemplateAuthorizationService
{
    public bool CanEdit(Template template) => template.Class == TemplateClass.Customized;
}

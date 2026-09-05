namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Template provenance (data-model.md §2.8, FR-7.1). <see cref="BuiltIn"/> templates are
/// platform-curated per tenant at provisioning and are <b>locked</b> — no persona may edit them;
/// <see cref="Customized"/> templates are the tenant's own, authored from a survey and editable.
/// Persisted as its PascalCase name (matching the <c>ck_templates_class</c> DDL CHECK).
/// </summary>
public enum TemplateClass
{
    BuiltIn,
    Customized,
}

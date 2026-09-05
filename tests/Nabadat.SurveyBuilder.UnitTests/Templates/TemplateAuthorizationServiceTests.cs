using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Templates;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Templates;

/// <summary>
/// T184 [US5] — write-first unit tests for <c>TemplateAuthorizationService</c> (T192). Built-in
/// templates are platform-curated and <b>locked</b> (FR-7.1): no persona may edit them — not even a
/// P-01 program manager. Customized templates are the tenant's own and are editable (persona-level
/// authorization to reach the edit endpoint is enforced separately at the API layer via
/// <c>template.write</c>, per contracts/templates.md; this class invariant is actor-independent, which
/// the P-01 case demonstrates).
/// <para>
/// Contract pinned for the implementer (T192):
/// <list type="bullet">
///   <item><c>TemplateAuthorizationService</c> lives in <c>Application/Templates/</c>; it is pure (no
///   I/O).</item>
///   <item><c>bool CanEdit(Template template)</c> → <c>false</c> when
///   <c>template.Class == TemplateClass.BuiltIn</c>, <c>true</c> when it is
///   <c>TemplateClass.Customized</c> (FR-7.1).</item>
///   <item><c>Template.Class</c> is added by T188 (the full entity); the <c>TemplateClass</c> enum
///   (<c>BuiltIn | Customized</c>) lands in <c>Domain/ValueObjects/</c>.</item>
/// </list>
/// Neither the service, the <c>Template.Class</c> column, nor the <c>TemplateClass</c> enum exists yet
/// → the project fails to COMPILE (valid red).
/// </para>
/// </summary>
public sealed class TemplateAuthorizationServiceTests
{
    private TemplateAuthorizationService CreateSut() => new();

    [Fact]
    public void CanEdit_is_false_for_a_built_in_template_regardless_of_persona()
    {
        var builtIn = new Template { Id = Guid.NewGuid(), Class = TemplateClass.BuiltIn, NameEn = "Banking CX baseline" };

        CreateSut().CanEdit(builtIn).Should().BeFalse();
    }

    [Fact]
    public void CanEdit_is_true_for_a_customized_template()
    {
        var customized = new Template { Id = Guid.NewGuid(), Class = TemplateClass.Customized, NameEn = "Our post-visit pulse" };

        CreateSut().CanEdit(customized).Should().BeTrue();
    }
}

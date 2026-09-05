using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Templates;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Templates;

/// <summary>
/// T185 [US5] — write-first unit tests for <c>TemplateSearchIndexer</c> (T193). The Templates tab
/// search matches a term against a template's <b>name OR any of its tags</b>, case-insensitively
/// (FR-6.2, key scenario "Tag search"). The in-process matcher pins the semantics; the EF/GIN query in
/// <c>TemplateSearchService</c> (T193) must return the same set.
/// <para>
/// Contract pinned for the implementer (T193):
/// <list type="bullet">
///   <item><c>TemplateSearchIndexer</c> lives in <c>Application/Templates/</c> and exposes
///   <c>static bool Match(string term, Template template)</c>.</item>
///   <item>Match is case-insensitive and substring-based over <c>template.NameEn</c> and every entry
///   of <c>template.Tags</c>.</item>
///   <item>An empty/whitespace term matches everything (an empty search box lists all templates).</item>
///   <item><c>Template.NameEn</c> and <c>Template.Tags</c> (a <c>string[]</c>) are added by T188.</item>
/// </list>
/// Neither <c>TemplateSearchIndexer</c> nor the <c>Template</c> name/tags columns exist yet → the
/// project fails to COMPILE (valid red).
/// </para>
/// </summary>
public sealed class TemplateSearchIndexerTests
{
    [Fact]
    public void Match_is_true_when_the_term_is_contained_in_the_name()
    {
        var template = new Template { Id = Guid.NewGuid(), Class = TemplateClass.Customized, NameEn = "Onboarding pulse", Tags = Array.Empty<string>() };

        TemplateSearchIndexer.Match("onboarding", template).Should().BeTrue();
    }

    [Fact]
    public void Match_is_true_when_the_term_is_contained_in_a_tag_but_not_the_name()
    {
        var template = new Template { Id = Guid.NewGuid(), Class = TemplateClass.Customized, NameEn = "Post-visit", Tags = new[] { "Onboarding" } };

        TemplateSearchIndexer.Match("onboarding", template).Should().BeTrue();
    }

    [Fact]
    public void Match_is_false_when_the_term_is_in_neither_the_name_nor_any_tag()
    {
        var template = new Template { Id = Guid.NewGuid(), Class = TemplateClass.Customized, NameEn = "Post-visit", Tags = new[] { "Branch" } };

        TemplateSearchIndexer.Match("onboarding", template).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_is_true_for_an_empty_or_whitespace_term(string term)
    {
        var template = new Template { Id = Guid.NewGuid(), Class = TemplateClass.Customized, NameEn = "Post-visit", Tags = new[] { "Branch" } };

        TemplateSearchIndexer.Match(term, template).Should().BeTrue();
    }
}

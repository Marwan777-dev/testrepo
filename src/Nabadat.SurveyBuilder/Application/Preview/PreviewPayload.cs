using Nabadat.SurveyBuilder.Application.Appearance;
using Nabadat.SurveyBuilder.Application.Translations;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Preview;

/// <summary>
/// The assembled F12 preview payload (Application-layer result of <see cref="PreviewPayloadBuilder"/>).
/// Carries the survey aggregate + its ordered sections/questions, the resolved appearance tokens
/// (Inherited or Customized), and the resolved locale bundle (target values + English fallback +
/// missing keys). The controller maps it to the wire <c>PreviewView</c>; the SPA wraps channel chrome
/// around it client-side (FR-12.1) and paginates by <see cref="Survey"/>.<c>Layout</c> (FR-12.3).
/// </summary>
public sealed record PreviewPayload(
    string Channel,
    string Locale,
    Survey Survey,
    ResolvedAppearance Theme,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<Question> Questions,
    ResolvedTranslationBundle Translations);

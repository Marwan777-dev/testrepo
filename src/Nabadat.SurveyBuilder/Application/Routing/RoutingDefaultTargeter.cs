using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>
/// T174 [US4] — computes the default routing target for an answer with no explicit override: the
/// next question in survey order (research.md §6). Defaults are computed, <b>never persisted</b> —
/// only overrides live in <c>routing_maps</c>, so a reorder recomputes defaults transparently. The
/// last question's default is end-of-survey (null). Pure — no I/O.
/// </summary>
public sealed class RoutingDefaultTargeter
{
    /// <summary>
    /// Returns <paramref name="nextInOrder"/>'s id when a next question exists, or null (⇒
    /// end-of-survey) when <paramref name="nextInOrder"/> is null.
    /// </summary>
    /// <param name="question">The source question (present for symmetry / future per-type rules).</param>
    /// <param name="nextInOrder">The question immediately after <paramref name="question"/>, or null.</param>
    public Guid? Default(Question question, Question? nextInOrder) => nextInOrder?.Id;
}

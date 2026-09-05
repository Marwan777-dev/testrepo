using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>Questions Set view (contracts/sections-and-sets.md). Exposes <see cref="RowVersion"/> for the ETag.</summary>
public sealed record QuestionsSetView(
    Guid Id,
    Guid SectionId,
    string Title,
    string? Description,
    QuestionsSetSelectionMode SelectionMode,
    int Count,
    int Order,
    int RowVersion)
{
    public static QuestionsSetView From(QuestionsSet s) =>
        new(s.Id, s.SectionId, s.Title, s.Description, s.SelectionMode, s.Count, s.Order, s.RowVersion);
}

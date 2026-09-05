using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Create / update / delete a question (T079). Enforces <see cref="QuestionValidator"/>,
/// <see cref="KpiBindingValidator"/> (shape) + the M-06 <see cref="IKpiCatalogReader"/> and M-16
/// <see cref="IJourneyReader"/> (cross-module existence/validity) on writes, and applies the comment
/// / sentiment flag policies. Cascade on delete (routing rows, translation-key scrub) is a US3/US6
/// concern — routing rows cascade at the DB level; the translation scrub is deferred (TODO-M01-003).
/// </summary>
public sealed class QuestionCommandService
{
    private readonly IQuestionStore _questions;
    private readonly QuestionValidator _validator;
    private readonly KpiBindingValidator _kpiValidator;
    private readonly CommentFieldFlagPolicy _comments;
    private readonly SentimentFlagPolicy _sentiment;
    private readonly IKpiCatalogReader _kpiCatalog;
    private readonly IJourneyReader _journeys;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public QuestionCommandService(
        IQuestionStore questions,
        QuestionValidator validator,
        KpiBindingValidator kpiValidator,
        CommentFieldFlagPolicy comments,
        SentimentFlagPolicy sentiment,
        IKpiCatalogReader kpiCatalog,
        IJourneyReader journeys,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _questions = questions;
        _validator = validator;
        _kpiValidator = kpiValidator;
        _comments = comments;
        _sentiment = sentiment;
        _kpiCatalog = kpiCatalog;
        _journeys = journeys;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Question> CreateAsync(QuestionWriteModel model, CancellationToken ct = default)
    {
        var binding = await ValidateAsync(model, ct);
        var now = _timeProvider.GetUtcNow();

        var question = new Question
        {
            Id = Guid.NewGuid(),
            SurveyId = model.SurveyId,
            SectionId = model.SectionId,
            SetId = model.SetId,
            Type = model.Type,
            Subtype = model.SubType,
            Text = model.Text,
            Description = model.Description,
            Required = model.Required,
            TypePayload = model.Payload,
            Order = model.Order,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ApplyFlags(question, model);
        ApplyBinding(question, binding);

        await _context.ExecuteAsync(async () => await _questions.AddAsync(question, ct), ct);
        return question;
    }

    public async Task<Question> UpdateAsync(Guid id, QuestionWriteModel model, CancellationToken ct = default)
    {
        var question = await _questions.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("question.not_found", 404, "Question not found.");

        var binding = await ValidateAsync(model, ct);
        var now = _timeProvider.GetUtcNow();

        question.Type = model.Type;
        question.Subtype = model.SubType;
        question.Text = model.Text;
        question.Description = model.Description;
        question.Required = model.Required;
        question.TypePayload = model.Payload;
        question.UpdatedAt = now;
        ApplyFlags(question, model);
        ApplyBinding(question, binding);
        question.IncrementRowVersion();

        await _context.ExecuteAsync(async () => await _questions.UpdateAsync(question, ct), ct);
        return question;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // routing_maps rows referencing this question cascade at the DB level (data-model.md §3.2);
        // the survey_translations key scrub is a US6 concern (TODO-M01-003).
        await _context.ExecuteAsync(async () => await _questions.DeleteAsync(id, ct), ct);
    }

    private async Task<KpiBinding?> ValidateAsync(QuestionWriteModel model, CancellationToken ct)
    {
        var result = _validator.Validate(model.ToDraft());
        if (!result.IsValid)
        {
            throw new SurveyBuilderException(result.Errors[0], 400, "The question is invalid.");
        }

        if (model.Binding is not { } binding)
        {
            return null;
        }

        if (!await _kpiCatalog.KpiExistsAsync(binding.KpiCode, ct))
        {
            throw new SurveyBuilderException("kpi.not_found", 400, "The KPI does not exist in the catalogue.");
        }

        var kpiResult = _kpiValidator.Validate(binding);
        if (!kpiResult.IsValid)
        {
            throw new SurveyBuilderException(kpiResult.Errors[0], 400, "The KPI binding is invalid.");
        }

        var normalised = kpiResult.Normalised;
        if (normalised.BoundJourneyOn
            && !await _journeys.IsBindingValidAsync(normalised.KpiCode, normalised.StageId, normalised.TouchpointId, ct))
        {
            throw new SurveyBuilderException("kpi.binding.invalid_for_journey", 400, "The stage/touchpoint is not valid for this KPI + journey.");
        }

        return normalised;
    }

    private void ApplyFlags(Question question, QuestionWriteModel model)
    {
        var comment = _comments.Apply(model.ShowComments);
        question.Comments = comment.HasCommentField;
        question.CommentLabel = comment.CommentLabel;
        question.CommentMaxLength = comment.CommentMaxLength;

        var sentiment = _sentiment.Apply(model.Type, model.SubType, model.Sentiment);
        question.Sentiment = sentiment.Applied;
    }

    private static void ApplyBinding(Question question, KpiBinding? binding)
    {
        question.KpiCode = binding?.KpiCode;
        question.Perspective = binding?.Perspective;
        question.BoundJourneyOn = binding?.BoundJourneyOn ?? true;
        question.StageId = binding?.StageId;
        question.TouchpointId = binding?.TouchpointId;
    }
}

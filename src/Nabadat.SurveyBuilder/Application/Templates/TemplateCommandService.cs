using System.Text.Json;
using Nabadat.SurveyBuilder.Application.Appearance.Interfaces;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Application.Templates.Dtos;
using Nabadat.SurveyBuilder.Application.Templates.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// Orchestrates the F6/F7 template lifecycle (T195): save-as-template (FR-7.4), metadata edit,
/// snapshot rebuild, delete (BR-7.1 no cascade to instantiated surveys), instantiate (FR-6.3), and
/// preview (FR-6.4). Built-in templates are read-only — every write path checks
/// <see cref="TemplateAuthorizationService.CanEdit"/> first (FR-7.1). The snapshot is a full copy of
/// the source survey (<see cref="TemplateSnapshotBuilder"/>) serialised to jsonb; instantiation
/// rebuilds a fresh Draft survey from it (<see cref="TemplateInstantiator"/>). Every compound write
/// runs inside <see cref="ITenantDbContext.ExecuteAsync"/>.
/// </summary>
public sealed class TemplateCommandService
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.General);

    private readonly ITemplateStore _templates;
    private readonly ISurveyStore _surveys;
    private readonly ISectionStore _sections;
    private readonly IQuestionStore _questions;
    private readonly IQuestionsSetStore _sets;
    private readonly IThemeStore _themes;
    private readonly IRoutingMapStore _routing;
    private readonly ITranslationStore _translations;
    private readonly TemplateAuthorizationService _authorization;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public TemplateCommandService(
        ITemplateStore templates,
        ISurveyStore surveys,
        ISectionStore sections,
        IQuestionStore questions,
        IQuestionsSetStore sets,
        IThemeStore themes,
        IRoutingMapStore routing,
        ITranslationStore translations,
        TemplateAuthorizationService authorization,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _templates = templates;
        _surveys = surveys;
        _sections = sections;
        _questions = questions;
        _sets = sets;
        _themes = themes;
        _routing = routing;
        _translations = translations;
        _authorization = authorization;
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task<Template?> GetAsync(Guid id, CancellationToken ct = default) => _templates.GetAsync(id, ct);

    /// <summary>Deserialised snapshot for the F6 preview / TemplateView summary; null if the template does not exist.</summary>
    public async Task<SurveySnapshot?> GetSnapshotAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _templates.GetSnapshotAsync(id, ct);
        return entity is null ? null : Deserialize(entity);
    }

    /// <summary>FR-7.4 — snapshot an existing survey (all data + bindings) into a new Customized template.</summary>
    public async Task<Template> CreateFromSurveyAsync(
        Guid sourceSurveyId, string nameEn, string? nameAr, string? description, string[] tags, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nameEn))
        {
            throw new SurveyBuilderException("template.name_en.required", 400, "Template English name is required.");
        }

        var source = await _surveys.GetAsync(sourceSurveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Source survey not found.");

        var now = _timeProvider.GetUtcNow();
        var snapshot = await BuildSnapshotAsync(source, ct);
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Class = TemplateClass.Customized,
            NameEn = nameEn,
            NameAr = nameAr,
            Description = description,
            Tags = tags,
            Sectors = Array.Empty<string>(),
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
            RowVersion = 1,
        };
        var snapshotEntity = new TemplateSnapshot
        {
            TemplateId = template.Id,
            Snapshot = JsonSerializer.Serialize(snapshot, SnapshotJson),
            SchemaVersion = snapshot.SchemaVersion,
            CreatedAt = now,
        };

        await _context.ExecuteAsync(() => _templates.AddAsync(template, snapshotEntity, ct), ct);
        return template;
    }

    public async Task<Template> UpdateAsync(Guid id, TemplatePatch patch, Guid actorId, CancellationToken ct = default)
    {
        var template = await _templates.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");
        EnsureEditable(template);

        if (patch.NameEn is not null)
        {
            template.NameEn = patch.NameEn;
        }

        if (patch.NameAr is not null)
        {
            template.NameAr = patch.NameAr;
        }

        if (patch.Description is not null)
        {
            template.Description = patch.Description;
        }

        if (patch.Tags is not null)
        {
            template.Tags = patch.Tags;
        }

        if (patch.PreviewThumbnailFileHandle is not null)
        {
            template.PreviewThumbnailFileHandle = patch.PreviewThumbnailFileHandle;
        }

        Stamp(template, actorId);
        await _context.ExecuteAsync(() => _templates.UpdateAsync(template, ct), ct);
        return template;
    }

    /// <summary>Refresh a Customized template's snapshot from an updated source survey.</summary>
    public async Task<Template> RebuildFromSurveyAsync(Guid id, Guid sourceSurveyId, Guid actorId, CancellationToken ct = default)
    {
        var template = await _templates.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");
        EnsureEditable(template);

        var source = await _surveys.GetAsync(sourceSurveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Source survey not found.");

        var snapshotEntity = await _templates.GetSnapshotAsync(id, ct)
            ?? throw new SurveyBuilderException("template.snapshot.missing", 500, "Template snapshot is missing.");

        var now = _timeProvider.GetUtcNow();
        var snapshot = await BuildSnapshotAsync(source, ct);
        snapshotEntity.Snapshot = JsonSerializer.Serialize(snapshot, SnapshotJson);
        snapshotEntity.SchemaVersion = snapshot.SchemaVersion;
        Stamp(template, actorId);

        await _context.ExecuteAsync(async () =>
        {
            await _templates.UpdateSnapshotAsync(snapshotEntity, ct);
            await _templates.UpdateAsync(template, ct);
        }, ct);
        return template;
    }

    /// <summary>Hard delete of the template + its snapshot; no cascade to already-instantiated surveys (BR-7.1).</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templates.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");
        EnsureEditable(template);

        await _context.ExecuteAsync(() => _templates.DeleteAsync(template, ct), ct);
    }

    /// <summary>FR-6.3 — create a fresh Draft survey owned by the caller from the template snapshot.</summary>
    public async Task<Survey> InstantiateAsync(Guid id, string? nameOverride, Guid callerId, CancellationToken ct = default)
    {
        var snapshotEntity = await _templates.GetSnapshotAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");

        var snapshot = Deserialize(snapshotEntity);
        var now = _timeProvider.GetUtcNow();
        var result = TemplateInstantiator.CreateSurveyFrom(snapshot, callerId, now);
        if (!string.IsNullOrWhiteSpace(nameOverride))
        {
            result.Survey.NameEn = nameOverride;
        }

        // Flush in FK-dependency order (survey → sections → sets → questions → theme/routing). EF has
        // no modelled relationships across these aggregates, so a single SaveChanges cannot topo-sort
        // the graph; the intermediate saves all run inside ExecuteAsync's open transaction, so the
        // whole instantiation stays atomic.
        await _context.ExecuteAsync(async () =>
        {
            await _surveys.AddAsync(result.Survey, ct);
            await _context.SaveChangesAsync(ct);

            foreach (var section in result.Sections)
            {
                await _sections.AddAsync(section, ct);
            }

            await _context.SaveChangesAsync(ct);

            foreach (var set in result.QuestionsSets)
            {
                await _sets.AddAsync(set, ct);
            }

            await _context.SaveChangesAsync(ct);

            foreach (var question in result.Questions)
            {
                await _questions.AddAsync(question, ct);
            }

            await _context.SaveChangesAsync(ct);

            if (result.Theme is { } theme)
            {
                await _themes.UpsertAsync(theme, ct);
            }

            foreach (var route in result.RoutingMaps)
            {
                await _routing.AddAsync(route, ct);
            }

            foreach (var translation in result.Translations)
            {
                await _translations.AddAsync(translation, ct);
            }

            await _context.SaveChangesAsync(ct);
        }, ct);

        return result.Survey;
    }

    /// <summary>FR-6.4 — a transient (not-persisted) survey rendered from the snapshot for preview.</summary>
    public async Task<Survey> BuildPreviewSurveyAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var snapshotEntity = await _templates.GetSnapshotAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");

        var snapshot = Deserialize(snapshotEntity);
        var now = _timeProvider.GetUtcNow();
        return TemplateInstantiator.CreateSurveyFrom(snapshot, actorId, now).Survey;
    }

    private async Task<SurveySnapshot> BuildSnapshotAsync(Survey source, CancellationToken ct)
    {
        var sections = await _sections.GetBySurveyAsync(source.Id, ct);
        var questions = await _questions.GetBySurveyAsync(source.Id, ct);
        var sets = new List<QuestionsSet>();
        foreach (var section in sections)
        {
            sets.AddRange(await _sets.GetBySectionAsync(section.Id, ct));
        }

        var theme = await _themes.GetBySurveyAsync(source.Id, ct);
        var routing = await _routing.GetBySurveyAsync(source.Id, ct);
        var translations = await _translations.GetBySurveyAsync(source.Id, ct);
        return TemplateSnapshotBuilder.Build(source, sections, questions, sets, theme, routing, translations);
    }

    private void EnsureEditable(Template template)
    {
        if (!_authorization.CanEdit(template))
        {
            throw new SurveyBuilderException("template.built_in_not_editable", 403, "Built-in templates cannot be edited.");
        }
    }

    private void Stamp(Template template, Guid actorId)
    {
        template.UpdatedBy = actorId;
        template.UpdatedAt = _timeProvider.GetUtcNow();
        template.IncrementRowVersion();
    }

    private static SurveySnapshot Deserialize(TemplateSnapshot entity) =>
        JsonSerializer.Deserialize<SurveySnapshot>(entity.Snapshot, SnapshotJson)
        ?? throw new SurveyBuilderException("template.snapshot.corrupt", 500, "Template snapshot could not be read.");
}

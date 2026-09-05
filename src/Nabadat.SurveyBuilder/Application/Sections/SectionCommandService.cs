using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Dtos;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Sections;

/// <summary>
/// Create / update a section (T147 support). Enforces <see cref="SectionValidator"/>; a create with
/// no explicit order appends to the end of the survey. Writes run inside
/// <see cref="ITenantDbContext.ExecuteAsync"/>. Deletion is handled by <see cref="SectionCascadeService"/>.
/// </summary>
public sealed class SectionCommandService
{
    private readonly ISectionStore _sections;
    private readonly SectionValidator _validator;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SectionCommandService(
        ISectionStore sections,
        SectionValidator validator,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _sections = sections;
        _validator = validator;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Section> CreateAsync(Guid? id, SectionWriteModel model, CancellationToken ct = default)
    {
        Validate(model);
        var now = _timeProvider.GetUtcNow();
        var order = model.Order ?? await _sections.CountBySurveyAsync(model.SurveyId, ct);

        var section = new Section
        {
            Id = id ?? Guid.NewGuid(),
            SurveyId = model.SurveyId,
            Name = model.Name,
            Description = model.Description,
            Order = order,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _context.ExecuteAsync(async () => await _sections.AddAsync(section, ct), ct);
        return section;
    }

    public async Task<Section> UpdateAsync(Guid id, SectionWriteModel model, CancellationToken ct = default)
    {
        var section = await _sections.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("section.not_found", 404, "Section not found.");

        Validate(model);

        section.Name = model.Name;
        section.Description = model.Description;
        if (model.Order is { } order)
        {
            section.Order = order;
        }

        section.UpdatedAt = _timeProvider.GetUtcNow();
        section.IncrementRowVersion();

        await _context.ExecuteAsync(async () => await _sections.UpdateAsync(section, ct), ct);
        return section;
    }

    private void Validate(SectionWriteModel model)
    {
        var result = _validator.Validate(new SectionDraft { Name = model.Name, Description = model.Description });
        if (!result.IsValid)
        {
            throw new SurveyBuilderException(result.Errors[0], 400, "The section is invalid.");
        }
    }
}

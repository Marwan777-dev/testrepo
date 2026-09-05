using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF value converter for <see cref="ActivePeriod"/> ↔ the <c>surveys.active_period</c> jsonb column
/// (data-model.md §2.1). Serialised as <c>{"days": int, "hours": int}</c> (camelCase); NULL ⇒ the
/// survey never auto-expires (FR-3.4). (T062 — kept in its own file per the one-type-per-file rule;
/// tasks.md named a combined <c>ValueConverters.cs</c>.)
/// </summary>
public sealed class ActivePeriodConverter : ValueConverter<ActivePeriod?, string?>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ActivePeriodConverter() : base(
        model => model == null ? null : JsonSerializer.Serialize(model, Options),
        provider => provider == null ? null : JsonSerializer.Deserialize<ActivePeriod>(provider, Options))
    {
    }
}

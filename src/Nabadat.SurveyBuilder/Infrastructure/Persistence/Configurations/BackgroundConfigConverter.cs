using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF value converter for <see cref="BackgroundConfig"/> ↔ the <c>themes.background_config</c> jsonb
/// column (data-model.md §2.6). Only the fields relevant to the sibling <c>background_type</c> are
/// populated; camelCase JSON. NULL ⇒ no per-type detail. (T062 — own file per the one-type-per-file
/// rule.)
/// </summary>
public sealed class BackgroundConfigConverter : ValueConverter<BackgroundConfig?, string?>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public BackgroundConfigConverter() : base(
        model => model == null ? null : JsonSerializer.Serialize(model, Options),
        provider => provider == null ? null : JsonSerializer.Deserialize<BackgroundConfig>(provider, Options))
    {
    }
}

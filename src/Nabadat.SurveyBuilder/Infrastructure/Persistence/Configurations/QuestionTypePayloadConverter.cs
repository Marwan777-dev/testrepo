using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF value converter for the polymorphic <see cref="QuestionTypePayload"/> ↔ the
/// <c>questions.type_payload</c> jsonb column (research.md §5). System.Text.Json writes the
/// <c>$type</c> discriminator declared on <see cref="QuestionTypePayload"/>, so each concrete
/// payload round-trips to the right record. (T062 — own file per the one-type-per-file rule.)
/// </summary>
public sealed class QuestionTypePayloadConverter : ValueConverter<QuestionTypePayload, string>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public QuestionTypePayloadConverter() : base(
        model => JsonSerializer.Serialize(model, Options),
        provider => JsonSerializer.Deserialize<QuestionTypePayload>(provider, Options)!)
    {
    }
}

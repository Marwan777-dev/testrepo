using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SurveyTranslation"/> to the tenant-schema <c>survey_translations</c> table (DB-08,
/// explicit columns — data-model.md §2.7). The <c>keys</c> column is <c>jsonb</c>, converted to/from
/// a <see cref="Dictionary{TKey,TValue}"/> via System.Text.Json (research.md §10). A
/// <see cref="ValueComparer{T}"/> is supplied because the mutable dictionary is a reference type —
/// without it EF cannot detect merge edits to the bundle. The unique <c>(survey_id, locale)</c> index
/// exists in <c>_Baseline.sql</c>; it is declared here too so the model matches the schema.
/// </summary>
public sealed class TranslationConfiguration : IEntityTypeConfiguration<SurveyTranslation>
{
    public void Configure(EntityTypeBuilder<SurveyTranslation> builder)
    {
        builder.ToTable("survey_translations");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.SurveyId).HasColumnName("survey_id");
        builder.Property(t => t.Locale).HasColumnName("locale");

        var keysConverter = new ValueConverter<Dictionary<string, string>, string>(
            dictionary => JsonSerializer.Serialize(dictionary, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)
                    ?? new Dictionary<string, string>());

        var keysComparer = new ValueComparer<Dictionary<string, string>>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null)
                             == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
            value => new Dictionary<string, string>(value));

        builder.Property(t => t.Keys)
            .HasColumnName("keys")
            .HasColumnType("jsonb")
            .HasConversion(keysConverter, keysComparer);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.RowVersion).HasColumnName("row_version");

        builder.HasIndex(t => new { t.SurveyId, t.Locale })
            .IsUnique()
            .HasDatabaseName("idx_survey_translations_survey_locale");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Survey"/> to the tenant-schema <c>surveys</c> table (DB-08, explicit columns —
/// data-model.md §2.1). Enums persist as their PascalCase name via <c>HasConversion&lt;string&gt;</c>
/// (matching the DDL CHECK constraints); <see cref="Survey.Layout"/> persists lowercase; the
/// <c>active_period</c> jsonb uses <see cref="ActivePeriodConverter"/>. <c>row_version</c> is the
/// app-managed ETag counter (research.md §2), not an EF concurrency token.
/// </summary>
public sealed class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> builder)
    {
        builder.ToTable("surveys");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.NameEn).HasColumnName("name_en");
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.SurveyType).HasColumnName("survey_type").HasConversion<string>();
        builder.Property(s => s.BoundJourneyId).HasColumnName("bound_journey_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(s => s.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(s => s.SubmittedBy).HasColumnName("submitted_by");
        builder.Property(s => s.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(s => s.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(s => s.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(s => s.ReviewRemarks).HasColumnName("review_remarks");
        builder.Property(s => s.ThemeMode).HasColumnName("theme_mode").HasConversion<string>();
        builder.Property(s => s.WelcomeHtml).HasColumnName("welcome_html");
        builder.Property(s => s.ThanksHtml).HasColumnName("thanks_html");
        builder.Property(s => s.SanitiserPolicyVersion).HasColumnName("sanitiser_policy_version");
        builder.Property(s => s.RedirectUrl).HasColumnName("redirect_url");
        builder.Property(s => s.RedirectAfterS).HasColumnName("redirect_after_s");

        // layout enum ↔ lowercase DDL values ('single' | 'section' | 'question' | 'count').
        builder.Property(s => s.Layout)
            .HasColumnName("layout")
            .HasConversion(
                mode => mode.ToString().ToLowerInvariant(),
                text => Enum.Parse<LayoutMode>(text, ignoreCase: true));

        builder.Property(s => s.QuestionsPerPage).HasColumnName("questions_per_page");
        builder.Property(s => s.ActivePeriod)
            .HasColumnName("active_period")
            .HasColumnType("jsonb")
            .HasConversion(new ActivePeriodConverter());
        builder.Property(s => s.ActivatedAt).HasColumnName("activated_at");
        builder.Property(s => s.RecordTime).HasColumnName("record_time");
        builder.Property(s => s.Shuffle).HasColumnName("shuffle");
        builder.Property(s => s.ShuffleMode).HasColumnName("shuffle_mode");
        builder.Property(s => s.RoutingOn).HasColumnName("routing_on");
        builder.Ignore(s => s.ShuffleLocked); // derived from RoutingOn (FR-9.1) — no column
        builder.Property(s => s.ThemeLogoFileHandle).HasColumnName("theme_logo_file_handle");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.RowVersion).HasColumnName("row_version");

        builder.HasIndex(s => new { s.Status, s.UpdatedAt }).HasDatabaseName("idx_surveys_status_updated_at");
        builder.HasIndex(s => s.BoundJourneyId).HasDatabaseName("idx_surveys_bound_journey_id");
        builder.HasIndex(s => s.OwnerUserId).HasDatabaseName("idx_surveys_owner_user_id");
    }
}

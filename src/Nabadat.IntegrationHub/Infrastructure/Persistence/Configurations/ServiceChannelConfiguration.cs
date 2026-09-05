using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ServiceChannel"/> to <c>service_channels</c> (DB-08: explicit
/// <c>HasColumnName</c> per property, onto the <c>IntegrationHub_Baseline.sql</c> schema).
/// <para>Not modelled here because EF cannot express them: the case-insensitive uniqueness of
/// <c>name_en</c> and <c>channel_id</c> (functional <c>LOWER(...)</c> unique indexes in the baseline,
/// VR-F02/VR-F04) and the literal-cased <c>channel_id</c> index the inbound resolve path uses.</para>
/// </summary>
public sealed class ServiceChannelConfiguration : IEntityTypeConfiguration<ServiceChannel>
{
    public void Configure(EntityTypeBuilder<ServiceChannel> builder)
    {
        builder.ToTable("service_channels");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.NameEn).HasColumnName("name_en");
        builder.Property(c => c.NameAr).HasColumnName("name_ar");
        builder.Property(c => c.ChannelId).HasColumnName("channel_id");
        builder.Property(c => c.Description).HasColumnName("description");
        builder.Property(c => c.Active).HasColumnName("active");
        builder.Property(c => c.ChannelIdLocked).HasColumnName("channel_id_locked");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}

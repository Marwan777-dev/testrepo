using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ChannelParameterAssignment"/> to <c>channel_parameter_assignments</c> — the channel
/// contract (BR-08). Carries the composite key <c>(service_channel_id, parameter_id)</c> and the two
/// intra-module FKs (Article 4.1); the FR-S4-04 "Required only while Supported" invariant is enforced by
/// the baseline CHECK plus the service layer's force-clear on the same write.
/// </summary>
public sealed class ChannelParameterAssignmentConfiguration : IEntityTypeConfiguration<ChannelParameterAssignment>
{
    public void Configure(EntityTypeBuilder<ChannelParameterAssignment> builder)
    {
        builder.ToTable("channel_parameter_assignments");

        builder.HasKey(a => new { a.ServiceChannelId, a.ParameterId });

        builder.Property(a => a.ServiceChannelId).HasColumnName("service_channel_id");
        builder.Property(a => a.ParameterId).HasColumnName("parameter_id");
        builder.Property(a => a.Supported).HasColumnName("supported");
        builder.Property(a => a.Required).HasColumnName("required");

        builder.HasOne<ServiceChannel>()
            .WithMany()
            .HasForeignKey(a => a.ServiceChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Parameter>()
            .WithMany()
            .HasForeignKey(a => a.ParameterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

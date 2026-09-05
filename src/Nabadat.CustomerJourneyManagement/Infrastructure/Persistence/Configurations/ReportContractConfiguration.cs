using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ReportContract"/> to the tenant-schema <c>report_contracts</c> table (DB-08,
/// explicit columns). <see cref="ReportContract.ContractPayload"/> is an opaque <c>jsonb</c>
/// payload read back verbatim by M-07.
/// </summary>
public sealed class ReportContractConfiguration : IEntityTypeConfiguration<ReportContract>
{
    public void Configure(EntityTypeBuilder<ReportContract> builder)
    {
        builder.ToTable("report_contracts");

        builder.HasKey(c => c.ReportContractId);

        builder.Property(c => c.ReportContractId).HasColumnName("report_contract_id").ValueGeneratedNever();
        builder.Property(c => c.JourneyId).HasColumnName("journey_id");
        builder.Property(c => c.ContractPayload).HasColumnName("contract_payload").HasColumnType("jsonb");
        builder.Property(c => c.GeneratedAt).HasColumnName("generated_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.JourneyId).IsUnique();
    }
}

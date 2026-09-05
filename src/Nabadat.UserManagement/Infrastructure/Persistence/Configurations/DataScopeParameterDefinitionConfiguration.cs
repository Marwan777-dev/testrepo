using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DataScopeParameterDefinition"/> to
/// <c>data_scope_parameter_definitions</c> (DB-08). The primary key is the
/// <c>parameter_name</c>; <c>allowed_values</c> is a <c>varchar[]</c>.</summary>
public sealed class DataScopeParameterDefinitionConfiguration : IEntityTypeConfiguration<DataScopeParameterDefinition>
{
    public void Configure(EntityTypeBuilder<DataScopeParameterDefinition> builder)
    {
        builder.ToTable("data_scope_parameter_definitions");

        builder.HasKey(d => d.ParameterName);

        builder.Property(d => d.ParameterName).HasColumnName("parameter_name").ValueGeneratedNever();
        builder.Property(d => d.Label).HasColumnName("label");
        builder.Property(d => d.AllowedValues)
            .HasColumnName("allowed_values")
            .HasConversion(UserManagementConverters.StringArray, UserManagementConverters.StringArrayComparer);
        builder.Property(d => d.SourceModule).HasColumnName("source_module");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
    }
}

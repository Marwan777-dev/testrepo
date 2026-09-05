using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Parameter"/> to <c>parameters</c> (DB-08). <c>data_type</c> and <c>origin</c> persist
/// as their snake_case wire values via <see cref="DataTypeConverter"/> / <see cref="ParameterOriginConverter"/>,
/// matching the baseline's CHECK constraints.
/// <para><c>Parameter.DataTypeLocked</c> is deliberately <b>ignored</b>: it is a derived projection of
/// <c>origin = 'built_in'</c> ([PO-G27], BR-09), not a column, so the lock can never drift from the
/// origin it expresses.</para>
/// </summary>
public sealed class ParameterConfiguration : IEntityTypeConfiguration<Parameter>
{
    public void Configure(EntityTypeBuilder<Parameter> builder)
    {
        builder.ToTable("parameters");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.NameEn).HasColumnName("name_en");
        builder.Property(p => p.NameAr).HasColumnName("name_ar");
        builder.Property(p => p.ApiField).HasColumnName("api_field");
        builder.Property(p => p.ApiFieldLocked).HasColumnName("api_field_locked");
        builder.Property(p => p.DataType).HasColumnName("data_type").HasConversion(new DataTypeConverter());
        builder.Property(p => p.RangeMin).HasColumnName("range_min");
        builder.Property(p => p.RangeMax).HasColumnName("range_max");
        builder.Property(p => p.RangeUnit).HasColumnName("range_unit");
        builder.Property(p => p.ValidationRule).HasColumnName("validation_rule");
        builder.Property(p => p.Origin).HasColumnName("origin").HasConversion(new ParameterOriginConverter());
        builder.Property(p => p.Enabled).HasColumnName("enabled");
        builder.Property(p => p.RequiredByDefault).HasColumnName("required_by_default");
        builder.Property(p => p.Filterable).HasColumnName("filterable");
        builder.Property(p => p.ReportingVisibility).HasColumnName("reporting_visibility");
        builder.Property(p => p.DashboardVisibility).HasColumnName("dashboard_visibility");
        builder.Property(p => p.MappingSupport).HasColumnName("mapping_support");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        // Derived from Origin — never persisted (see the class remarks).
        builder.Ignore(p => p.DataTypeLocked);
    }
}

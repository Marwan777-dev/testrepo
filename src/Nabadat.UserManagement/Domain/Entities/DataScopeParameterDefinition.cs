namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// An M-13-supplied scope parameter and its full set of valid values
/// (tenant-schema table <c>data_scope_parameter_definitions</c>). A
/// <see cref="DataScopeAssignment"/> may only grant a subset of these values.
/// </summary>
public sealed class DataScopeParameterDefinition
{
    /// <summary>Unique within the tenant schema (PK).</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>All valid values for this parameter.</summary>
    public IReadOnlyList<string> AllowedValues { get; set; } = [];

    /// <summary>Source module that supplied the definition (default <c>M-13</c>).</summary>
    public string SourceModule { get; set; } = "M-13";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

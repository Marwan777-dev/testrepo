namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// Parameter-based allowed values (sourced from M-13) assigned to a user
/// (tenant-schema table <c>data_scope_assignments</c>). Unique per
/// <c>(user_id, parameter_name)</c>.
/// </summary>
public sealed class DataScopeAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Must exist in <c>data_scope_parameter_definitions</c>.</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Subset of the parameter's allowed values.</summary>
    public IReadOnlyList<string> AllowedValues { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

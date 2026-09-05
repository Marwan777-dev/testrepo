namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>Wire-format mapping for <see cref="PermissionMode"/>.</summary>
public static class PermissionModeExtensions
{
    public static string ToWire(this PermissionMode mode) => mode.ToString();

    public static PermissionMode ParseMode(string wire) =>
        Enum.TryParse<PermissionMode>(wire, ignoreCase: false, out var mode)
            ? mode
            : throw new ArgumentException($"Unknown permission mode '{wire}'.", nameof(wire));
}

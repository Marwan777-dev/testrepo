namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>Wire-format mapping for <see cref="UserStatus"/> (varchar(32) column).</summary>
public static class UserStatusExtensions
{
    public static string ToWire(this UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Inactive => "inactive",
        UserStatus.Locked => "locked",
        UserStatus.PendingEnrollment => "pending-enrollment",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown user status."),
    };

    public static UserStatus ParseStatus(string wire) => wire switch
    {
        "active" => UserStatus.Active,
        "inactive" => UserStatus.Inactive,
        "locked" => UserStatus.Locked,
        "pending-enrollment" => UserStatus.PendingEnrollment,
        _ => throw new ArgumentException($"Unknown user status '{wire}'.", nameof(wire)),
    };

    /// <summary>Parses a wire status without throwing; returns false for an unknown value.</summary>
    public static bool TryParseStatus(string wire, out UserStatus status)
    {
        switch (wire)
        {
            case "active": status = UserStatus.Active; return true;
            case "inactive": status = UserStatus.Inactive; return true;
            case "locked": status = UserStatus.Locked; return true;
            case "pending-enrollment": status = UserStatus.PendingEnrollment; return true;
            default: status = default; return false;
        }
    }
}

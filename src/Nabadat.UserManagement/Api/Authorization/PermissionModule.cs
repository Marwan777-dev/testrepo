namespace Nabadat.UserManagement.Api.Authorization;

/// <summary>
/// The canonical DOC-02 permission modules a <see cref="RequirePermissionAttribute"/> can gate on.
/// Each member name is <b>exactly</b> the module id stored in the permission snapshot
/// (<see cref="Domain.ValueObjects.PermissionSnapshot.Modules"/>) and the persona baseline, so the
/// gate resolves the wire key via <see cref="System.Enum.ToString()"/> with no lookup table — keep
/// the names byte-for-byte aligned with the seeded module ids.
/// </summary>
public enum PermissionModule
{
    /// <summary>CX-domain: survey authoring (P-01 exclusive).</summary>
    SurveyBuilder,

    /// <summary>CX-domain: channel configuration (P-01 exclusive).</summary>
    ChannelManagement,

    /// <summary>CX-domain: audience / contact management (P-01 exclusive).</summary>
    AudienceManagement,

    /// <summary>CX-domain: analytics and reporting (P-01 exclusive).</summary>
    AnalyticsAndReporting,

    /// <summary>CX-domain: closed-loop case management (P-01 exclusive).</summary>
    CaseManagement,

    /// <summary>CX-domain: alerts and notifications (P-01 exclusive).</summary>
    AlertsAndNotifications,

    /// <summary>CX-domain: KPI catalogue + configuration — gates the M-06 endpoints (P-01 manages; P-02/P-06 view).</summary>
    KpiConfiguration,

    /// <summary>Non-CX: user administration (P-01 / P-07).</summary>
    UserManagement,

    /// <summary>Non-CX: tenant configuration (P-01 / P-07).</summary>
    TenantConfiguration,
}

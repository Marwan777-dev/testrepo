using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.KpiTypes;

/// <summary>
/// The KPI-type catalog application service (T052 / US-2). Backs <c>GET|POST /api/v1/kpi-types</c>
/// (<c>contracts/configuration-api.md</c>): the tenant's available KPI types are the six
/// platform-standard built-ins plus any tenant-defined custom types stored in
/// <c>kpi_type_definitions</c>.
/// <list type="bullet">
///   <item><description>
///     <b>Platform-standard types are reference data, not rows.</b> The six built-ins
///     (<see cref="PlatformKpiType"/>) are never stored in <c>kpi_type_definitions</c>; their
///     labels and default scoring direction live in <see cref="PlatformStandardCatalog"/> here, the
///     single backend source for the <c>platformStandardTypes</c> list.
///   </description></item>
///   <item><description>
///     <b>Create validates then guards uniqueness.</b> The <c>typeKey</c> format (1–64 chars,
///     alphanumeric + underscore), required labels, and a known scoring direction are checked first
///     (<c>kpi_type.validation_error</c>); a key that collides with a platform-standard key
///     (case-insensitive) or an existing tenant key (<see cref="IKpiTypeDataService.ExistsByKeyAsync"/>)
///     is rejected with <c>kpi_type.key_conflict</c> before any write.
///   </description></item>
/// </list>
/// Creating a KPI type emits no M-17 event (none is defined for it in the contract) and needs no
/// transaction — it is a single insert (<see cref="IKpiTypeDataService.CreateAsync"/>).
/// </summary>
public sealed class KpiTypeService
{
    /// <summary>
    /// The six platform-standard KPI types with their bilingual labels and default scoring direction,
    /// exactly as <c>contracts/configuration-api.md §GET /api/v1/kpi-types</c> specifies. All are
    /// <c>Ascending</c> except <c>CES</c> (<c>Descending</c> — lower effort is better). Keys match the
    /// <see cref="PlatformKpiType"/> member names.
    /// </summary>
    public static readonly IReadOnlyList<PlatformKpiTypeInfo> PlatformStandardCatalog =
    [
        new PlatformKpiTypeInfo("NPS", "صافي نقاط الترويج", "Net Promoter Score", "Ascending"),
        new PlatformKpiTypeInfo("CSAT", "رضا العملاء", "Customer Satisfaction", "Ascending"),
        new PlatformKpiTypeInfo("CES", "جهد العميل", "Customer Effort Score", "Descending"),
        new PlatformKpiTypeInfo("FCR", "الحل من أول مرة", "First Contact Resolution", "Ascending"),
        new PlatformKpiTypeInfo("AgentSatisfaction", "رضا الموظف", "Agent Satisfaction", "Ascending"),
        new PlatformKpiTypeInfo("VFM", "القيمة مقابل المال", "Value for Money", "Ascending"),
    ];

    /// <summary>Platform-standard keys for the create-conflict guard (case-insensitive, mirrors the enum).</summary>
    private static readonly HashSet<string> PlatformStandardKeys =
        new(Enum.GetNames<PlatformKpiType>(), StringComparer.OrdinalIgnoreCase);

    private const int MaxTypeKeyLength = 64;

    private readonly IKpiTypeDataService _kpiTypes;
    private readonly TimeProvider _time;

    public KpiTypeService(IKpiTypeDataService kpiTypes, TimeProvider time)
    {
        _kpiTypes = kpiTypes;
        _time = time;
    }

    /// <summary>Returns the tenant-defined KPI types (the platform-standard catalog is static reference data).</summary>
    public Task<IReadOnlyList<KpiTypeDefinition>> ListTenantDefinedAsync(CancellationToken ct = default)
        => _kpiTypes.ListAsync(ct);

    /// <summary>
    /// Creates a tenant-defined KPI type. Returns the persisted definition on success, or a failure
    /// carrying <c>kpi_type.validation_error</c> (bad <c>typeKey</c> format, missing label, or unknown
    /// scoring direction) or <c>kpi_type.key_conflict</c> (key already used by a platform-standard or
    /// tenant-defined type). No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult<KpiTypeDefinition>> CreateAsync(
        CreateKpiTypeInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var typeKey = input.TypeKey?.Trim() ?? string.Empty;
        if (!IsValidTypeKey(typeKey))
        {
            return ServiceResult<KpiTypeDefinition>.Failure(
                "kpi_type.validation_error",
                $"typeKey must be 1–{MaxTypeKeyLength} characters using letters, digits, or underscore only.");
        }

        if (string.IsNullOrWhiteSpace(input.LabelAr) || string.IsNullOrWhiteSpace(input.LabelEn))
        {
            return ServiceResult<KpiTypeDefinition>.Failure(
                "kpi_type.validation_error", "Both labelAr and labelEn are required.");
        }

        var direction = string.IsNullOrWhiteSpace(input.ScoringDirection)
            ? nameof(ScoringDirection.Ascending)
            : input.ScoringDirection;
        if (!Enum.TryParse<ScoringDirection>(direction, ignoreCase: true, out var parsedDirection))
        {
            return ServiceResult<KpiTypeDefinition>.Failure(
                "kpi_type.validation_error", "scoringDirection must be 'Ascending' or 'Descending'.");
        }

        // Conflict guard runs before any write: a tenant type may not shadow a platform-standard key,
        // nor reuse a key already defined for this tenant.
        if (PlatformStandardKeys.Contains(typeKey))
        {
            return ServiceResult<KpiTypeDefinition>.Failure(
                "kpi_type.key_conflict", $"'{typeKey}' is a reserved platform-standard KPI type.");
        }

        if (await _kpiTypes.ExistsByKeyAsync(typeKey, ct))
        {
            return ServiceResult<KpiTypeDefinition>.Failure(
                "kpi_type.key_conflict", $"A KPI type with key '{typeKey}' already exists.");
        }

        var now = _time.GetUtcNow();
        var definition = new KpiTypeDefinition
        {
            KpiTypeDefinitionId = Guid.NewGuid(),
            TypeKey = typeKey,
            LabelAr = input.LabelAr.Trim(),
            LabelEn = input.LabelEn.Trim(),
            // Normalise to the canonical PascalCase member name regardless of request casing.
            ScoringDirection = parsedDirection.ToString(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _kpiTypes.CreateAsync(definition, ct);
        return ServiceResult<KpiTypeDefinition>.Success(definition);
    }

    /// <summary>typeKey rule: 1–64 chars, ASCII letters/digits/underscore only (no regex — a simple scan).</summary>
    private static bool IsValidTypeKey(string typeKey)
    {
        if (typeKey.Length is 0 or > MaxTypeKeyLength)
        {
            return false;
        }

        foreach (var c in typeKey)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// A platform-standard KPI type for the <c>GET /api/v1/kpi-types</c> <c>platformStandardTypes</c> list:
/// the built-in key plus its bilingual labels and default scoring direction.
/// </summary>
public sealed record PlatformKpiTypeInfo(string TypeKey, string LabelAr, string LabelEn, string ScoringDirection);

/// <summary>
/// Create-KPI-type input (<c>POST /api/v1/kpi-types</c>). <see cref="ScoringDirection"/> defaults to
/// <c>Ascending</c> when null/blank; <see cref="TypeKey"/> is validated and uniqueness-checked by
/// <see cref="KpiTypeService.CreateAsync"/>.
/// </summary>
public sealed record CreateKpiTypeInput(string TypeKey, string LabelAr, string LabelEn, string? ScoringDirection);

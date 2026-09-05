using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Organization.Interfaces;

/// <summary>
/// The single source of truth for the canonical tenant industry list (FR-050 / R13). M-06-internal
/// (re-homed from the never-built M-11, 2026-06-24). Supplies the dropdown options and validates a
/// candidate industry string pre-write.
/// </summary>
public interface IIndustryEnumProvider
{
    /// <summary>The canonical six industries, in canonical (render/serialisation) order.</summary>
    IReadOnlyList<Industry> GetAll();

    /// <summary>True when <paramref name="industry"/> matches a canonical industry name (case-sensitive,
    /// matching the enum member names); false for null/empty/unknown.</summary>
    bool IsValid(string? industry);
}

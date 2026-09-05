namespace Nabadat.KpiManagement.Domain.ValueObjects;

/// <summary>
/// The canonical tenant industry list (FR-050 / R13). M-06 is the single source of truth — the
/// six members in canonical order match what M-11 tenant provisioning will consume once that module
/// is built (re-homed from the never-built M-11 to M-06, 2026-06-24). The order here is the order
/// the Industry dropdown renders and <c>industry_options</c> serialises.
/// </summary>
public enum Industry
{
    Banking,
    Telecommunications,
    Government,
    Automotive,
    Entertainment,
    Services,
}

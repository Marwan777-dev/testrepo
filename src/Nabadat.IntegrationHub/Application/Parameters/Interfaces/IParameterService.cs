using Nabadat.IntegrationHub.Application.Parameters.Dtos;

namespace Nabadat.IntegrationHub.Application.Parameters.Interfaces;

/// <summary>
/// The parameter-catalogue aggregate (US2): SCR-05's list and SCR-06's drawer. This interface is the mock seam
/// for the sub-domain — the individual rules it composes are concrete types with no second implementation.
///
/// <para><b>There is no delete member and there never will be</b> (BR-09): a parameter of either origin is
/// disabled, never removed, and its API field name stays reserved forever (VR-F06).</para>
/// </summary>
public interface IParameterService
{
    /// <summary>Creates a custom parameter (<c>POST .../parameters</c>). Built-ins are seeded, never created.</summary>
    Task<ParameterSaveResult> CreateAsync(ParameterCreateCommand command, CancellationToken ct = default);

    /// <summary>
    /// Applies a partial update (<c>PATCH .../parameters/{id}</c>) — SCR-05's inline enable/disable toggle and
    /// SCR-06's edit drawer share this path. A disable on a referenced parameter returns BR-10's reference list
    /// <b>without applying the change</b> until the command carries the confirmation flag.
    /// </summary>
    Task<ParameterSaveResult> PatchAsync(Guid id, ParameterPatchCommand command, CancellationToken ct = default);

    /// <summary>One AND-combined, cursor-paginated page of SCR-05's list, plus the global origin-tab counts.</summary>
    Task<ParameterPage> ListAsync(
        ParameterListFilter? filter = null,
        string? cursor = null,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>One parameter for SCR-06's drawer; <c>null</c> when it does not exist.</summary>
    Task<ParameterDto?> GetAsync(Guid id, CancellationToken ct = default);
}

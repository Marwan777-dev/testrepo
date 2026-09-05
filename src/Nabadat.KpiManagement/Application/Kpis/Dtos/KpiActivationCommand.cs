namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// The unit of work for <c>KpiActivationCommandHandler.HandleAsync</c> (FR-026): flip a KPI's Active
/// state, with the binding-aware deactivation confirmation. <see cref="Confirm"/> is honoured only on
/// the deactivation path — when the KPI is bound and not yet confirmed, the handler returns the
/// binding-usage counts instead of writing. <see cref="ActorId"/> / <see cref="ActorPersona"/> /
/// <see cref="CorrelationId"/> are carried onto the M-17 audit event.
/// </summary>
/// <param name="KpiId">The KPI to activate / deactivate.</param>
/// <param name="Active">Target Active state (true = activate, false = deactivate).</param>
/// <param name="Confirm">When deactivating a bound KPI, the explicit confirmation that bypasses the gate.</param>
/// <param name="ActorId">M-10 user id of the actor (audit attribution).</param>
/// <param name="ActorPersona">Actor persona <c>P-01</c>..<c>P-08</c>.</param>
/// <param name="CorrelationId">Per-request correlation id stamped on the audit event.</param>
public sealed record KpiActivationCommand(
    Guid KpiId,
    bool Active,
    bool Confirm,
    Guid ActorId,
    string ActorPersona,
    Guid CorrelationId);

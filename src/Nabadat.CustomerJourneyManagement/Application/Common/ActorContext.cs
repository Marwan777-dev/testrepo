namespace Nabadat.CustomerJourneyManagement.Application.Common;

/// <summary>
/// The authenticated caller behind a mutating M-16 operation, threaded from the API layer
/// (resolved from the JWT — API-02) down into the application services. Services stamp
/// <see cref="UserId"/> onto <c>updated_by</c>/<c>created_by</c> columns and copy
/// <see cref="Persona"/> + <see cref="CorrelationId"/> onto every M-17 audit event they
/// publish, so each <c>event_log</c> row records who acted, in which persona, and under
/// which request correlation.
/// </summary>
/// <param name="UserId">M-10 <c>user_id</c> of the caller (no FK across modules).</param>
/// <param name="Persona">Caller persona tag <c>P-01</c>..<c>P-08</c> (e.g. <c>P-01</c>).</param>
/// <param name="CorrelationId">Per-request correlation id, carried onto emitted events for tracing.</param>
public sealed record ActorContext(Guid UserId, string Persona, Guid CorrelationId);

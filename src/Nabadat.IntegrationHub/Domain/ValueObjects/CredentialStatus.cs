namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// Credential lifecycle state (data-model.md §10). The transition is <b>one-way</b>: there is no
/// un-revoke, and a credential's plaintext is never retrievable after its show-once dialog closes.
/// Generating a new credential atomically revokes the current <see cref="Active"/> one (BR-16), so at
/// most one Active row exists per integration; revoked rows are retained for audit, never deleted.
/// <para>Persisted as <c>active</c> / <c>revoked</c> via <c>CredentialStatusConverter</c>.</para>
/// </summary>
public enum CredentialStatus
{
    Active = 1,

    Revoked = 2,
}

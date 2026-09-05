namespace Nabadat.UserManagement.Domain.Interfaces;

/// <summary>
/// <b>Consumed interface — owned by M-09 (Notifications).</b> M-10 calls this
/// <i>synchronously</i> during password reset; if delivery throws, the caller's
/// transaction rolls back and the reset token is never persisted (FR-021).
///
/// Defined as the consumer-side port because the M-09 module is not yet present in
/// this solution; no production implementation is registered until M-09 ships.
/// </summary>
public interface IM09NotificationService
{
    /// <summary>Delivers a password-reset message carrying the raw (un-hashed) token.</summary>
    Task SendPasswordResetAsync(string email, string rawToken, CancellationToken ct = default);
}

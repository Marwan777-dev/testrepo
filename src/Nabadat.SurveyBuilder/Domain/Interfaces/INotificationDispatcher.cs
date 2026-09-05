namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-09 (Alerts &amp; Notifications)</b> to broadcast a
/// notification to every tenant user holding a given permission (architecture Article 3,
/// published-interface only). Used by the approval orchestrator (T118) to notify reviewers when a
/// survey is submitted for review (FR-15.2, Q7 broadcast fan-out). Parameters are BCL primitives so
/// this port stays free of Application types (Domain references nothing).
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-09 (which does
/// not exist under <c>src/</c> yet) and wired in the host composition root. No US2 runtime path
/// resolves it until then.</para>
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Notify every user in <paramref name="scope"/> holding <paramref name="permission"/>, linking to
    /// <paramref name="deepLink"/> and rendering <paramref name="template"/>.
    /// </summary>
    Task BroadcastAsync(string scope, string permission, string deepLink, string template, CancellationToken ct);
}

using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// The survey aggregate root (tenant-schema table <c>surveys</c>, data-model.md §2.1). Owns its
/// settings (F3), appearance mode (F4), lifecycle status (Status Transition Matrix), and the
/// monotonic <see cref="RowVersion"/> ETag counter (research.md §2). Cross-module identifiers
/// (<see cref="BoundJourneyId"/>, owner/reviewer user ids) are opaque — no FKs (Article 4.1).
/// </summary>
public sealed class Survey
{
    public Guid Id { get; set; }

    /// <summary>English survey name, 1–200 chars (<c>survey.name_en.required/max_length</c>).</summary>
    public string NameEn { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Derived from <see cref="BoundJourneyId"/> (BR-3.3) — see <c>SurveyTypeSyncService</c>.</summary>
    public SurveyType SurveyType { get; set; } = SurveyType.SeasonalRelational;

    /// <summary>M-16 journey id; null ⇒ <see cref="SurveyType.SeasonalRelational"/>. No FK.</summary>
    public Guid? BoundJourneyId { get; set; }

    public SurveyStatus Status { get; set; } = SurveyStatus.Draft;

    /// <summary>M-10 user id of the author-of-record; scopes the "Publish own surveys" grant (Q8).</summary>
    public Guid OwnerUserId { get; set; }

    public Guid? SubmittedBy { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewRemarks { get; set; }

    public ThemeMode ThemeMode { get; set; } = ThemeMode.Inherited;

    /// <summary>Sanitised at ingress (Q3); the applied policy is recorded in <see cref="SanitiserPolicyVersion"/>.</summary>
    public string? WelcomeHtml { get; set; }

    public string? ThanksHtml { get; set; }

    /// <summary>Which sanitiser allowlist version cleaned <see cref="WelcomeHtml"/>/<see cref="ThanksHtml"/> (Q3 audit).</summary>
    public int SanitiserPolicyVersion { get; set; } = 1;

    public string? RedirectUrl { get; set; }

    public int RedirectAfterS { get; set; }

    public LayoutMode Layout { get; set; } = LayoutMode.Section;

    /// <summary>Present only when <see cref="Layout"/> is <see cref="LayoutMode.Count"/>; must be ≥ 1.</summary>
    public int? QuestionsPerPage { get; set; }

    /// <summary>How long the survey collects after being sent; null ⇒ never auto-expires (FR-3.4).</summary>
    public ActivePeriod? ActivePeriod { get; set; }

    /// <summary>
    /// The "start" instant of the active-period lifecycle (FR-3.4 / BR-3.4): when the survey most
    /// recently entered <see cref="SurveyStatus.Active"/>. Null until first published; stamped by
    /// <see cref="ChangeStatus"/> on every transition into Active (a Pause→Reactivate is a fresh
    /// start). Combined with <see cref="ActivePeriod"/> to derive the absolute expiry M-04 enforces
    /// (see <c>ActiveSurveyReader</c>).
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>System-managed; always true (FR-3.5).</summary>
    public bool RecordTime { get; set; } = true;

    public bool Shuffle { get; set; }

    /// <summary><c>random</c> | <c>low_response</c>. Mutually exclusive with <see cref="RoutingOn"/>.</summary>
    public string ShuffleMode { get; set; } = "random";

    /// <summary>Requires <see cref="LayoutMode.Question"/> and disables/locks <see cref="Shuffle"/> (F9).</summary>
    public bool RoutingOn { get; set; }

    /// <summary>
    /// Whether <see cref="Shuffle"/> is locked in the builder — true exactly while routing is on
    /// (FR-9.1). Derived from <see cref="RoutingOn"/>, so it stays correct across reloads and needs
    /// no column of its own (EF ignores it — see <c>SurveyConfiguration</c>).
    /// </summary>
    public bool ShuffleLocked => RoutingOn;

    /// <summary>Opaque file-storage handle for the F4 survey logo; null when none uploaded.</summary>
    public string? ThemeLogoFileHandle { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid UpdatedBy { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2). Default 1; bumped on every write.</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>
    /// Factory for a fresh Draft survey with its journey↔type invariant applied (BR-3.3). The
    /// caller sets settings fields afterwards; <c>SurveyValidator</c> validates before persist.
    /// </summary>
    public static Survey Create(Guid id, string nameEn, Guid ownerUserId, Guid? boundJourneyId, Guid actorId, DateTimeOffset now)
    {
        var survey = new Survey
        {
            Id = id,
            NameEn = nameEn,
            OwnerUserId = ownerUserId,
            BoundJourneyId = boundJourneyId,
            SurveyType = boundJourneyId is null ? SurveyType.SeasonalRelational : SurveyType.Transactional,
            Status = SurveyStatus.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        return survey;
    }

    /// <summary>
    /// Applies a validated status transition. Transition legality (Status Transition Matrix,
    /// BR-1.4) and side-effects (publish gate, purge) are enforced by the Application services
    /// before this is called; here it only records the new state, stamps the writer/time, and — on
    /// entry into Active — stamps <see cref="ActivatedAt"/> as the active-period start (FR-3.4).
    /// </summary>
    public void ChangeStatus(SurveyStatus next, Guid actorId, DateTimeOffset now)
    {
        Status = next;
        if (next == SurveyStatus.Active)
        {
            // FR-3.4 "start" instant — the active period is measured from each entry into Active.
            ActivatedAt = now;
        }

        UpdatedBy = actorId;
        UpdatedAt = now;
        IncrementRowVersion();
    }

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}

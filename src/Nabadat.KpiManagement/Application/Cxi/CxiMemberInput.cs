namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// Input to <see cref="CxiSnapshotComposer.Compose"/>: one CXI member's identity, its normalised raw
/// <see cref="Score"/> (0–100), and its relative integer <see cref="Weight"/>. The composer derives
/// each member's effective percentage from the weights — it is not supplied here.
/// </summary>
public sealed record CxiMemberInput(Guid KpiId, string KpiShortName, int Weight, decimal Score);

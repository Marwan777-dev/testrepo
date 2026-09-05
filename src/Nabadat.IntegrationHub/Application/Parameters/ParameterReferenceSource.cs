namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// One candidate reference handed to <see cref="ParameterDisableImpactScanner"/> — which parameter it points at
/// and the human-readable name D-6 shows the user ("Self-Service Kiosk", "Eastern Region Analysts", …).
///
/// <para>It deliberately carries <b>no</b> <see cref="ParameterReferenceKind"/>: the kind is stamped by which
/// argument the candidate arrived in, so a caller cannot mislabel a channel contract as a scope filter.</para>
/// </summary>
/// <param name="ParameterId">The M-13 parameter this reference points at.</param>
/// <param name="Name">The consumer's display name, as shown in the impact warning.</param>
public sealed record ParameterReferenceSource(Guid ParameterId, string Name);

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// One resolved reference to a parameter, as listed in BR-10's impact warning (Dialog D-6) and returned in the
/// <c>PATCH .../parameters/{id}</c> response so the console can render the warning before the user confirms.
/// </summary>
/// <param name="Kind">Which consumer family holds the reference.</param>
/// <param name="Name">The consumer's display name.</param>
public sealed record ParameterReference(ParameterReferenceKind Kind, string Name);

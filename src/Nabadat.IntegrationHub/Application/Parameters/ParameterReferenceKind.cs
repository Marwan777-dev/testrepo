namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// The three consumer families BR-10's impact warning (Dialog D-6) can name. The order of the members is the
/// order references are listed in, so D-6's copy is deterministic across calls.
/// </summary>
public enum ParameterReferenceKind
{
    /// <summary>A service channel's parameter contract (M-13's own <c>channel_parameter_assignments</c>).</summary>
    ChannelContract = 1,

    /// <summary>An M-10 data-scope filter built on this parameter's definition and value set (CMC-06).</summary>
    DataScopeFilter = 2,

    /// <summary>An M-14 / M-15 / M-16 rule or action referencing this parameter (CMC-07).</summary>
    RuleBuilder = 3,
}

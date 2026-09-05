using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists <see cref="Scenario"/> as its snake_case wire value (data-model.md §1, matching the
/// <c>ck_integrations_scenario</c> CHECK and the inbound API's wire vocabulary) rather than the
/// PascalCase member name a bare <c>HasConversion&lt;string&gt;()</c> would emit.
/// <para>The map is explicit, not derived from the member name: <c>OAuthClient → oauth_client</c> in the
/// sibling <see cref="CredentialMechanismConverter"/> proves a naive PascalCase→snake_case rule is
/// wrong, so every enum in this module gets a hand-written map.</para>
/// </summary>
public sealed class ScenarioConverter : ValueConverter<Scenario, string>
{
    public ScenarioConverter() : base(v => ToWire(v), v => FromWire(v))
    {
    }

    private static string ToWire(Scenario value) => value switch
    {
        Scenario.Dispatch => "dispatch",
        Scenario.RedirectLink => "redirect_link",
        Scenario.JsonRender => "json_render",
        Scenario.IframeEmbed => "iframe_embed",
        Scenario.ResponseIngestion => "response_ingestion",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown scenario."),
    };

    private static Scenario FromWire(string value) => value switch
    {
        "dispatch" => Scenario.Dispatch,
        "redirect_link" => Scenario.RedirectLink,
        "json_render" => Scenario.JsonRender,
        "iframe_embed" => Scenario.IframeEmbed,
        "response_ingestion" => Scenario.ResponseIngestion,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown scenario wire value."),
    };
}

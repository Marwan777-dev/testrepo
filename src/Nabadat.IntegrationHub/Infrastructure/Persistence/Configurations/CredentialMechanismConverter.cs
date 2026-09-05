using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists <see cref="CredentialMechanism"/> as <c>api_key</c> / <c>oauth_client</c> (data-model.md §2,
/// matching the <c>ck_credentials_mechanism</c> CHECK). Note <c>OAuthClient → oauth_client</c>: a naive
/// PascalCase→snake_case rule would produce <c>o_auth_client</c>, which is why this module maps every
/// enum explicitly instead of deriving the wire value.
/// </summary>
public sealed class CredentialMechanismConverter : ValueConverter<CredentialMechanism, string>
{
    public CredentialMechanismConverter() : base(v => ToWire(v), v => FromWire(v))
    {
    }

    private static string ToWire(CredentialMechanism value) => value switch
    {
        CredentialMechanism.ApiKey => "api_key",
        CredentialMechanism.OAuthClient => "oauth_client",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown credential mechanism."),
    };

    private static CredentialMechanism FromWire(string value) => value switch
    {
        "api_key" => CredentialMechanism.ApiKey,
        "oauth_client" => CredentialMechanism.OAuthClient,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown credential mechanism wire value."),
    };
}

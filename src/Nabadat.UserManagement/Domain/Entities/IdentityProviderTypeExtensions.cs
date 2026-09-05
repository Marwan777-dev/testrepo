namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>Wire-format mapping for <see cref="IdentityProviderType"/>.</summary>
public static class IdentityProviderTypeExtensions
{
    public static string ToWire(this IdentityProviderType type) => type switch
    {
        IdentityProviderType.Directory => "directory",
        IdentityProviderType.GoogleOidc => "google-oidc",
        IdentityProviderType.Internal => "internal",
        IdentityProviderType.Saml2 => "saml2",
        IdentityProviderType.Nafath => "nafath",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown provider type."),
    };

    public static IdentityProviderType ParseProviderType(string wire) => wire switch
    {
        "directory" => IdentityProviderType.Directory,
        "google-oidc" => IdentityProviderType.GoogleOidc,
        "internal" => IdentityProviderType.Internal,
        "saml2" => IdentityProviderType.Saml2,
        "nafath" => IdentityProviderType.Nafath,
        _ => throw new ArgumentException($"Unknown provider type '{wire}'.", nameof(wire)),
    };
}

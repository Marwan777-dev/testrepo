namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// Supported SSO provider types. Additive/extensible without a migration
/// (stored as <c>varchar(32)</c>); wire form is the kebab-case string.
/// </summary>
public enum IdentityProviderType
{
    Directory,
    GoogleOidc,
    Internal,
    Saml2,
    Nafath,
}

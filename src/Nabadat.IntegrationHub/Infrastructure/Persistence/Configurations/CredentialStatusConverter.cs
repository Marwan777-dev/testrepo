using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists <see cref="CredentialStatus"/> as <c>active</c> / <c>revoked</c> (data-model.md §2, matching
/// the <c>ck_credentials_status</c> CHECK and the partial unique index that enforces BR-16's
/// one-active-credential-per-integration invariant).
/// </summary>
public sealed class CredentialStatusConverter : ValueConverter<CredentialStatus, string>
{
    public CredentialStatusConverter() : base(v => ToWire(v), v => FromWire(v))
    {
    }

    private static string ToWire(CredentialStatus value) => value switch
    {
        CredentialStatus.Active => "active",
        CredentialStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown credential status."),
    };

    private static CredentialStatus FromWire(string value) => value switch
    {
        "active" => CredentialStatus.Active,
        "revoked" => CredentialStatus.Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown credential status wire value."),
    };
}

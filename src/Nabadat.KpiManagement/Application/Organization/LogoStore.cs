using Microsoft.Extensions.Configuration;
using Nabadat.KpiManagement.Application.Organization.Interfaces;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// Filesystem-backed <see cref="ILogoStore"/> (the on-prem mode of research.md R3 / AD-05). Stores
/// each tenant's logo under <c>{basePath}/tenants/{tenantId}/branding/logo.{ext}</c>, where the base
/// path comes from <c>LogoStorage:BasePath</c> (default: <c>App_Data/logos</c> under the host content
/// root). The SaaS object-store implementation would swap in behind the same interface without any
/// caller change. Bytes live on disk, never in the tenant DB.
/// </summary>
public sealed class LogoStore : ILogoStore
{
    private readonly string _basePath;

    public LogoStore(IConfiguration configuration)
    {
        _basePath = configuration["LogoStorage:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "logos");
    }

    public async Task<LogoBlobRef> PutAsync(Guid tenantId, string contentType, Stream payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var key = StorageKeyFor(tenantId, contentType);
        var fullPath = Path.Combine(_basePath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // Replace any prior logo (a different extension would otherwise orphan the old file).
        foreach (var stale in Directory.EnumerateFiles(Path.GetDirectoryName(fullPath)!, "logo.*"))
        {
            File.Delete(stale);
        }

        await using (var file = File.Create(fullPath))
        {
            await payload.CopyToAsync(file, ct);
        }

        return new LogoBlobRef(key.Replace(Path.DirectorySeparatorChar, '/'));
    }

    public Task<Stream> GetAsync(LogoBlobRef blobRef, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(blobRef);

        var fullPath = Path.Combine(_basePath, blobRef.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    /// <summary>Maps an accepted content type to the storage-key extension used in the blob path.</summary>
    public static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/svg+xml" => "svg",
        _ => "bin",
    };

    /// <summary>Maps a storage-key extension back to its content type (for the GET logo response).</summary>
    public static string ContentTypeFor(string storageKey) =>
        Path.GetExtension(storageKey).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };

    private static string StorageKeyFor(Guid tenantId, string contentType) =>
        Path.Combine("tenants", tenantId.ToString(), "branding", $"logo.{ExtensionFor(contentType)}");
}

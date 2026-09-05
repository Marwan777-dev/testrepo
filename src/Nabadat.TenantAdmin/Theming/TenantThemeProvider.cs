using System.Text.Json;

namespace Nabadat.TenantAdmin.Theming;

/// <summary>
/// Resolves a tenant's brand-theme seed from the <c>tenant-themes.json</c> file (subdomain → colors),
/// with an in-code default for any subdomain not listed. The file is read once at construction and
/// cached (it is small and changes rarely; a restart picks up edits). A missing or malformed file is
/// tolerated — every subdomain then falls back to <see cref="Default"/>. Registered as a singleton.
/// </summary>
public sealed class TenantThemeProvider
{
    /// <summary>The in-code default coloring (the pinned Nabadat brand) used when a subdomain has no
    /// entry in the JSON file.</summary>
    public static readonly ThemeColors Default =
        new("#0D8BBC", "#13DB9B", "#1E2235", "#1E2235", "#EEF1F7", "#F7F9FC");

    private const string FileName = "tenant-themes.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyDictionary<string, ThemeColors> _bySlug;

    public TenantThemeProvider(IHostEnvironment environment, ILogger<TenantThemeProvider> logger)
    {
        // Read from the content root (project dir at dev, publish dir in prod) so the JSON is found
        // without depending on bin-copy semantics. tenant-themes.json lives under Theming/.
        var path = Path.Combine(environment.ContentRootPath, "Theming", FileName);
        _bySlug = Load(path, logger);
    }

    /// <summary>
    /// Returns the colors mapped to <paramref name="slug"/>, or <see cref="Default"/> when the slug is
    /// null/blank or has no entry. <paramref name="isDefault"/> reports which case applied so the
    /// caller can echo the right slug to the frontend (default → keep the pinned index.css).
    /// </summary>
    public ThemeColors Resolve(string? slug, out bool isDefault)
    {
        if (!string.IsNullOrWhiteSpace(slug) && _bySlug.TryGetValue(slug, out var colors))
        {
            isDefault = false;
            return colors;
        }

        isDefault = true;
        return Default;
    }

    private static IReadOnlyDictionary<string, ThemeColors> Load(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            logger.LogWarning("[Theming] {File} not found at {Path}; every tenant uses the default theme.", FileName, path);
            return Empty();
        }

        try
        {
            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, ThemeColors?>>(json, JsonOptions);
            if (raw is null)
            {
                return Empty();
            }

            // Drop the "//" comment key and any null/incomplete entries; key the map case-insensitively
            // (subdomains are lower-cased before lookup, but be defensive).
            var map = new Dictionary<string, ThemeColors>(StringComparer.OrdinalIgnoreCase);
            foreach (var (slug, colors) in raw)
            {
                if (slug.StartsWith("//", StringComparison.Ordinal) || colors is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(colors.Primary)
                    || string.IsNullOrWhiteSpace(colors.Secondary)
                    || string.IsNullOrWhiteSpace(colors.Neutral))
                {
                    logger.LogWarning("[Theming] Skipping '{Slug}': primary/secondary/neutral are all required.", slug);
                    continue;
                }

                map[slug] = colors;
            }

            logger.LogInformation("[Theming] Loaded {Count} tenant theme(s) from {File}.", map.Count, FileName);
            return map;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogError(ex, "[Theming] Failed to read {File}; every tenant uses the default theme.", FileName);
            return Empty();
        }
    }

    private static IReadOnlyDictionary<string, ThemeColors> Empty() =>
        new Dictionary<string, ThemeColors>(StringComparer.OrdinalIgnoreCase);
}

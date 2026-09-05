using System.Globalization;
using System.Text;

namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Opaque cursor for the catalogue list (research.md R8): a base64-encoded <c>&lt;iso8601&gt;|&lt;uuid&gt;</c>
/// tuple of the last returned row's <c>(created_at, id)</c>. <see cref="TryDecode"/> never throws —
/// a malformed or absent cursor simply yields <see langword="false"/> (treated as "first page").
/// </summary>
public static class KpiCatalogueCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            return DateTimeOffset.TryParse(
                       parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out createdAt)
                   && Guid.TryParse(parts[1], out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

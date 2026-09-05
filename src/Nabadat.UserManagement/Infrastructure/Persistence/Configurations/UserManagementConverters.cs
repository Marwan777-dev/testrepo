using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared EF Core value converters + comparers for the M-10 model (DB-08). Two recurring
/// shapes the raw-Npgsql repositories handled by hand:
/// <list type="bullet">
///   <item><b>Postgres arrays</b> (<c>varchar[]</c>) ↔ <see cref="IReadOnlyList{String}"/> —
///   Npgsql maps the provider <c>string[]</c> to the array column natively.</item>
///   <item><b>jsonb</b> ↔ arbitrary CLR shapes (records, dictionaries) via System.Text.Json
///   with Web defaults (matching the prior repositories' <c>JsonSerializerDefaults.Web</c>).</item>
/// </list>
/// Each converter ships a <see cref="ValueComparer{T}"/> so EF change-tracking detects
/// mutations of these reference-typed properties.
/// </summary>
public static class UserManagementConverters
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><see cref="IReadOnlyList{String}"/> ↔ <c>string[]</c> (Postgres <c>varchar[]</c>).</summary>
    public static readonly ValueConverter<IReadOnlyList<string>, string[]> StringArray = new(
        model => model.ToArray(),
        provider => provider.ToList());

    public static readonly ValueComparer<IReadOnlyList<string>> StringArrayComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        v => v.ToList());

    /// <summary>A jsonb converter for an arbitrary serializable shape <typeparamref name="T"/>.</summary>
    public static ValueConverter<T, string> Jsonb<T>() => new(
        model => JsonSerializer.Serialize(model, Json),
        provider => JsonSerializer.Deserialize<T>(provider, Json)!);

    /// <summary>A snapshotting comparer for a jsonb-backed shape (serialize-equality + deep copy).</summary>
    public static ValueComparer<T> JsonbComparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
        v => JsonSerializer.Serialize(v, Json).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!);
}

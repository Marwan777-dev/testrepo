using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// BR-27 / <c>[PO-G25]</c> — the mapping-support state machine, keyed on the data type. Extracted from
/// <see cref="ParameterService"/> (T057) so the rule lives in exactly one place: the same three branches govern
/// the create path, the patch path, and SCR-06's flag rendering.
///
/// <list type="bullet">
///   <item><see cref="DataType.List"/> — always <c>true</c>, <b>locked</b>. Membership is never enforced at
///   ingestion (BR-12), so the mapping table is the sole source of a List's values.</item>
///   <item><see cref="DataType.Text"/>, <see cref="DataType.Boolean"/>, <see cref="DataType.Url"/> — available,
///   <b>user-changeable</b>, default <c>false</c>.</item>
///   <item>every other type — always <c>false</c>, <b>locked</b> (unavailable).</item>
/// </list>
///
/// <para>Enforced <b>server-side even if a client sends a contradicting value</b> (data-model.md §4): the
/// submitted flag is an input to <see cref="Resolve"/>, never the stored value. The baseline's
/// <c>ck_parameters_mapping_support_by_type</c> CHECK backs the same rule, so a bug here surfaces as a constraint
/// violation rather than bad data.</para>
/// </summary>
public static class MappingSupportPolicy
{
    /// <summary>True when the user may choose the flag for this type; false when it is forced either way.</summary>
    public static bool IsChangeable(DataType dataType) =>
        dataType is DataType.Text or DataType.Boolean or DataType.Url;

    /// <summary>
    /// The value to persist, given what the client asked for. A <c>null</c> request means "not submitted" and
    /// falls back to the type's default.
    /// </summary>
    public static bool Resolve(DataType dataType, bool? requested) => dataType switch
    {
        DataType.List => true,
        _ when IsChangeable(dataType) => requested ?? false,
        _ => false,
    };
}

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// The stable error codes the parameter-catalogue write path emits (T051–T057), and which
/// <c>ParametersController</c> maps to HTTP statuses per contracts/api-endpoints.md. Codes — not messages — are
/// the contract: the console copy in <see cref="ParameterValidationError.Message"/> may be reworded or localised
/// without breaking a client, the code may not.
///
/// <para>Status mapping owned by the controller: <see cref="DuplicateApiField"/>, <see cref="ApiFieldLocked"/>
/// and <see cref="ParameterTypeLocked"/> → <b>409</b>, <see cref="ParameterNotFound"/> → <b>404</b>, every other
/// <c>validation.*</c> → <b>400</b>.</para>
/// </summary>
public static class ParameterErrorCodes
{
    /// <summary>VR-F05 — Parameter name · EN is required.</summary>
    public const string NameEnRequired = "validation.name_en_required";

    /// <summary>VR-F05 — Parameter name · EN exceeds <see cref="ParameterNameValidator.MaxNameLength"/>.</summary>
    public const string NameEnTooLong = "validation.name_en_too_long";

    /// <summary>VR-F05 — Parameter name · AR is required.</summary>
    public const string NameArRequired = "validation.name_ar_required";

    /// <summary>VR-F05 — Parameter name · AR exceeds <see cref="ParameterNameValidator.MaxNameLength"/>.</summary>
    public const string NameArTooLong = "validation.name_ar_too_long";

    /// <summary>VR-F06 — the API field name is required.</summary>
    public const string ApiFieldRequired = "validation.api_field_required";

    /// <summary>VR-F06 / BR-11 — the API field name is not <c>snake_case</c> (baseline <c>^[a-z][a-z0-9_]*$</c>).</summary>
    public const string ApiFieldFormat = "validation.api_field_format";

    /// <summary>
    /// VR-F06 — another parameter already reserves this API field name, <b>including a disabled or built-in
    /// one</b>. → 409.
    /// </summary>
    public const string DuplicateApiField = "validation.duplicate_api_field";

    /// <summary>
    /// BR-11 — the API field name is locked by the first request that carried it (built-ins: always locked,
    /// BR-09) and can no longer change. → 409.
    /// </summary>
    public const string ApiFieldLocked = "parameter.api_field_locked";

    /// <summary>VR-F07 — Minimum is required for a Range parameter.</summary>
    public const string RangeMinRequired = "validation.range_min_required";

    /// <summary>VR-F07 — Maximum is required for a Range parameter.</summary>
    public const string RangeMaxRequired = "validation.range_max_required";

    /// <summary>VR-F07 — Minimum must be strictly less than Maximum.</summary>
    public const string RangeMinMax = "validation.range_min_max";

    /// <summary>
    /// The submitted range configuration does not belong to the selected data type — mirrors the baseline's
    /// <c>ck_parameters_range_only_for_range</c> CHECK so switching Range → List without clearing the card is an
    /// inline error rather than a database exception.
    /// </summary>
    public const string RangeNotApplicable = "validation.range_not_applicable";

    /// <summary><c>[PO-G27]</c> / BR-09 — a built-in parameter's data type is read-only. → 409.</summary>
    public const string ParameterTypeLocked = "parameter.type_locked";

    /// <summary>VR-F13 — the tenant is already at its NFR-16 ceiling of <b>custom</b> parameters.</summary>
    public const string CapacityExceeded = "validation.capacity_exceeded";

    /// <summary>The submitted <c>data_type</c> is not one of the 13 ratified types (FR-F0-04, <c>[PO-G17]</c>).</summary>
    public const string InvalidDataType = "validation.invalid_data_type";

    /// <summary>A submitted channel assignment references a service channel that does not exist.</summary>
    public const string UnknownChannel = "validation.unknown_channel";

    /// <summary>The addressed parameter does not exist. → 404.</summary>
    public const string ParameterNotFound = "parameter.not_found";
}

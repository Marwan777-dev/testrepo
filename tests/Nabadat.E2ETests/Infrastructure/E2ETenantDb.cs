using System.Text.RegularExpressions;
using Npgsql;

namespace Nabadat.E2ETests.Infrastructure;

/// <summary>
/// Minimal direct-SQL access to the running backend's tenant schema, used ONLY by the T127a
/// deactivation-confirmation E2E to seed the one precondition no portal UI can create: an M-16
/// <c>kpi_bindings</c> row whose <c>kpi_id</c> equals a custom KPI's GUID (the binding-usage probe
/// counts by id; the journey-builder UI binds by KPI-*type*). Mirrors the integration fixture's
/// <c>BindKpiToTouchpointAsync</c> / <c>SeedBoundTouchpointAsync</c>, schema-qualified for the
/// multi-tenant dev host (<c>tenant_{slug}</c>).
///
/// <para>Configured via <see cref="E2ESettings.TenantDb"/> + <see cref="E2ESettings.TenantSchema"/>
/// (gitignored <c>appsettings.local.json</c> / <c>E2E_TENANT_DB</c> env). When no connection string
/// is set, <see cref="IsConfigured"/> is false and the one dependent test skips with a clear reason
/// — the browser-only scenarios are unaffected.</para>
/// </summary>
internal sealed class E2ETenantDb
{
    private static readonly Regex SafeSchema = new("^[A-Za-z_][A-Za-z0-9_]*$");

    private readonly string _connectionString;
    private readonly string _schema;

    public E2ETenantDb(E2ESettings settings)
    {
        _connectionString = settings.TenantDb;
        _schema = settings.TenantSchema;
        if (!string.IsNullOrEmpty(_schema) && !SafeSchema.IsMatch(_schema))
        {
            throw new ArgumentException($"Unsafe tenant schema identifier: '{_schema}'.");
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    /// <summary>Resolves a KPI's id by Short Name (case-insensitive) in the tenant schema; null if absent.</summary>
    public async Task<Guid?> GetKpiIdByShortNameAsync(string shortName)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT id FROM {_schema}.kpi_definitions WHERE LOWER(short_name) = LOWER(@sn)", connection);
        command.Parameters.AddWithValue("sn", shortName);
        return await command.ExecuteScalarAsync() is Guid g ? g : null;
    }

    /// <summary>
    /// Seeds a non-archived journey → stage → touchpoint chain and binds <paramref name="kpiId"/> to
    /// the touchpoint (a <c>kpi_bindings</c> row carrying the logical <c>kpi_id</c>), so the M-06
    /// binding-usage probe reports the KPI as used by one touchpoint in one journey. Returns the new
    /// journey id (delete it to cascade-remove the stage, touchpoint, and binding).
    /// </summary>
    public async Task<Guid> SeedBoundTouchpointAsync(Guid kpiId)
    {
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();

        await using var connection = await OpenAsync();

        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.journeys (journey_id, name, journey_type, status, created_by, created_at, updated_at)
            VALUES (@j, @name, 'Transactional', 'Active', @actor, now(), now())
            """,
            ("j", journeyId), ("name", $"E2E deactivation journey {journeyId:N}"), ("actor", Guid.Empty));

        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.stages (stage_id, journey_id, sequence_number, name, created_at, updated_at)
            VALUES (@s, @j, 1, 'Stage', now(), now())
            """,
            ("s", stageId), ("j", journeyId));

        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.touchpoints (touchpoint_id, stage_id, name, created_at, updated_at)
            VALUES (@t, @s, 'Touchpoint', now(), now())
            """,
            ("t", touchpointId), ("s", stageId));

        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.kpi_bindings
                (kpi_binding_id, touchpoint_id, kpi_type, is_platform_standard, kpi_id, weight, created_at, updated_at)
            VALUES (@bid, @tid, 'custom', false, @kid, 100, now(), now())
            """,
            ("bid", Guid.NewGuid()), ("tid", touchpointId), ("kid", kpiId));

        return journeyId;
    }

    /// <summary>Deletes a seeded journey (cascades to its stages, touchpoints, and KPI bindings).</summary>
    public async Task DeleteJourneyAsync(Guid journeyId)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, $"DELETE FROM {_schema}.journeys WHERE journey_id = @j", ("j", journeyId));
    }

    /// <summary>Deletes a custom KPI created by a test (its threshold row, then the definition).</summary>
    public async Task DeleteKpiAsync(Guid kpiId)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, $"DELETE FROM {_schema}.kpi_thresholds WHERE kpi_id = @id", ("id", kpiId));
        await ExecuteAsync(connection, $"DELETE FROM {_schema}.kpi_definitions WHERE id = @id", ("id", kpiId));
    }

    /// <summary>
    /// Seeds an M-13 service channel directly (SCR-03/04 E2E). Two things the console UI cannot
    /// produce are set here: <paramref name="channelIdLocked"/> — BR-05's one-way lock, which the
    /// product sets only on a channel's first 2xx inbound request (a pipeline US4 owns) — and a
    /// deterministic starting row for the duplicate-name assertion, so that test does not have to
    /// create a channel through the UI just to collide with it.
    /// </summary>
    public async Task<Guid> SeedServiceChannelAsync(
        string nameEn,
        string nameAr,
        string channelId,
        bool channelIdLocked = false,
        string? description = null)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.service_channels
                (id, name_en, name_ar, channel_id, description, active, channel_id_locked, created_at, updated_at)
            VALUES (@id, @en, @ar, @cid, @desc, true, @locked, now(), now())
            """,
            ("id", id), ("en", nameEn), ("ar", nameAr), ("cid", channelId),
            ("desc", (object?)description ?? DBNull.Value), ("locked", channelIdLocked));
        return id;
    }

    /// <summary>
    /// Removes a channel a test created, by id or by channel ID (case-insensitive). E2E writes are
    /// real rows with no transaction rollback, and VR-F13 caps a tenant at 100 channels — without
    /// this the shared e2e tenant walks into the ceiling after enough runs (TODO-M13-004). Deleting
    /// here is fixture hygiene, not a product capability: BR-07 means no DELETE endpoint exists.
    /// </summary>
    public async Task DeleteServiceChannelAsync(Guid id)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection,
            $"DELETE FROM {_schema}.channel_parameter_assignments WHERE service_channel_id = @id", ("id", id));
        await ExecuteAsync(connection, $"DELETE FROM {_schema}.service_channels WHERE id = @id", ("id", id));
    }

    /// <summary>Removes a channel by its channel ID (case-insensitive); no-op when absent.</summary>
    public async Task DeleteServiceChannelByChannelIdAsync(string channelId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT id FROM {_schema}.service_channels WHERE LOWER(channel_id) = LOWER(@cid)", connection);
        command.Parameters.AddWithValue("cid", channelId);
        if (await command.ExecuteScalarAsync() is Guid id)
        {
            await DeleteServiceChannelAsync(id);
        }
    }

    /// <summary>
    /// Seeds an M-13 <b>custom</b> parameter directly (SCR-05/06 E2E). Two things the console UI
    /// cannot produce on demand are what this exists for: a parameter that is already
    /// <c>enabled = false</c> (VR-F06's uniqueness must bite against a disabled row too, and
    /// creating-then-disabling through the UI would make the duplicate test depend on the very
    /// toggle a different test owns), and a deterministic custom Range row for the AND-filter
    /// assertion. Built-ins are NOT seeded here — the baseline ships all 23.
    /// </summary>
    public async Task<Guid> SeedParameterAsync(
        string nameEn,
        string nameAr,
        string apiField,
        string dataType = "text",
        bool enabled = true,
        decimal? rangeMin = null,
        decimal? rangeMax = null,
        string? rangeUnit = null)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.parameters
                (id, name_en, name_ar, api_field, api_field_locked, data_type,
                 range_min, range_max, range_unit, origin, enabled,
                 required_by_default, filterable, reporting_visibility, dashboard_visibility,
                 mapping_support, created_at, updated_at)
            VALUES (@id, @en, @ar, @field, false, @type,
                    @min, @max, @unit, 'custom', @enabled,
                    false, true, true, false,
                    @mapping, now(), now())
            """,
            ("id", id), ("en", nameEn), ("ar", nameAr), ("field", apiField), ("type", dataType),
            ("min", (object?)rangeMin ?? DBNull.Value),
            ("max", (object?)rangeMax ?? DBNull.Value),
            ("unit", (object?)rangeUnit ?? DBNull.Value),
            ("enabled", enabled),
            // BR-27 — the CHECK constraint forces `list` on and every non-opt-in type off.
            ("mapping", dataType == "list"));
        return id;
    }

    /// <summary>
    /// Adds the parameter to a channel's contract — the reference BR-10's impact warning (Dialog
    /// D-6) reports. Written directly rather than through the drawer's channel pills so the
    /// impact-warning test asserts the dialog, not the assignment path a different test owns.
    /// </summary>
    public async Task AssignParameterToChannelAsync(Guid serviceChannelId, Guid parameterId, bool required = false)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection,
            $"""
            INSERT INTO {_schema}.channel_parameter_assignments
                (service_channel_id, parameter_id, supported, required)
            VALUES (@cid, @pid, true, @req)
            ON CONFLICT (service_channel_id, parameter_id)
            DO UPDATE SET supported = true, required = EXCLUDED.required
            """,
            ("cid", serviceChannelId), ("pid", parameterId), ("req", required));
    }

    /// <summary>
    /// Removes a parameter a test created or seeded, with its dependent rows. Fixture hygiene, not
    /// a product capability: BR-09 means no DELETE endpoint exists for a parameter at all, and
    /// VR-F13 caps a tenant at 200 custom parameters — without this the shared e2e tenant walks
    /// into that ceiling (the same failure mode as TODO-M13-004's channels).
    /// </summary>
    public async Task DeleteParameterAsync(Guid id)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection,
            $"DELETE FROM {_schema}.channel_parameter_assignments WHERE parameter_id = @id", ("id", id));
        await ExecuteAsync(connection,
            $"DELETE FROM {_schema}.parameter_mappings WHERE parameter_id = @id", ("id", id));
        await ExecuteAsync(connection,
            $"DELETE FROM {_schema}.unmapped_value_occurrences WHERE parameter_id = @id", ("id", id));
        // Guarded on origin: a bug in a test must never be able to delete one of the 23 built-ins.
        await ExecuteAsync(connection,
            $"DELETE FROM {_schema}.parameters WHERE id = @id AND origin = 'custom'", ("id", id));
    }

    /// <summary>Removes a <b>custom</b> parameter by its API field; no-op when absent or built-in.</summary>
    public async Task DeleteCustomParameterByApiFieldAsync(string apiField)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT id FROM {_schema}.parameters WHERE api_field = @field AND origin = 'custom'", connection);
        command.Parameters.AddWithValue("field", apiField);
        if (await command.ExecuteScalarAsync() is Guid id)
        {
            await DeleteParameterAsync(id);
        }
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] args)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}

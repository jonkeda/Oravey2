using Microsoft.Data.Sqlite;

namespace Oravey2.Core.Data;

public sealed record MapMarkerRecord(long Id, long RegionId, int GridX, int GridY, string Label, string? Icon);

public sealed class SaveStateStore : IDisposable
{
    private readonly SqliteConnection _connection;

    public SaveStateStore(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        DatabaseInitializer.InitializeSave(_connection);
    }

    /// <summary>For testing with an already-open in-memory connection.</summary>
    internal SaveStateStore(SqliteConnection connection)
    {
        _connection = connection;
        DatabaseInitializer.InitializeSave(_connection);
    }

    // ── Party ──────────────────────────────────────────────

    public void SaveParty(string dataJson)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM party;
            INSERT INTO party (data_json) VALUES ($json);
            """;
        cmd.Parameters.AddWithValue("$json", dataJson);
        cmd.ExecuteNonQuery();
    }

    public string? LoadParty()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT data_json FROM party LIMIT 1;";
        return cmd.ExecuteScalar() as string;
    }

    // ── Chunk State ────────────────────────────────────────

    public void SaveChunkState(long regionId, int gridX, int gridY, byte[]? tileOverrides, string? modifiedEntities = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO chunk_state (region_id, grid_x, grid_y, tile_overrides, modified_entities)
            VALUES ($rid, $gx, $gy, $to, $me)
            ON CONFLICT(region_id, grid_x, grid_y) DO UPDATE
                SET tile_overrides = excluded.tile_overrides,
                    modified_entities = excluded.modified_entities;
            """;
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        cmd.Parameters.AddWithValue("$to", (object?)tileOverrides ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$me", (object?)modifiedEntities ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public (byte[]? TileOverrides, string? ModifiedEntities)? GetChunkState(long regionId, int gridX, int gridY)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT tile_overrides, modified_entities FROM chunk_state WHERE region_id = $rid AND grid_x = $gx AND grid_y = $gy;";
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return (r.IsDBNull(0) ? null : (byte[])r[0], r.IsDBNull(1) ? null : r.GetString(1));
    }

    // ── Fog of War ─────────────────────────────────────────

    public void SaveFogOfWar(long regionId, int gridX, int gridY, bool revealed)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO fog_of_war (region_id, grid_x, grid_y, revealed)
            VALUES ($rid, $gx, $gy, $rev)
            ON CONFLICT(region_id, grid_x, grid_y) DO UPDATE SET revealed = excluded.revealed;
            """;
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        cmd.Parameters.AddWithValue("$rev", revealed ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public bool? GetFogOfWar(long regionId, int gridX, int gridY)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT revealed FROM fog_of_war WHERE region_id = $rid AND grid_x = $gx AND grid_y = $gy;";
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        var result = cmd.ExecuteScalar();
        return result is long val ? val != 0 : null;
    }

    // ── Discovered POI ─────────────────────────────────────

    public void DiscoverPoi(long poiId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO discovered_poi (poi_id) VALUES ($pid);";
        cmd.Parameters.AddWithValue("$pid", poiId);
        cmd.ExecuteNonQuery();
    }

    public List<long> GetDiscoveredPois()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT poi_id FROM discovered_poi;";
        using var r = cmd.ExecuteReader();
        var results = new List<long>();
        while (r.Read()) results.Add(r.GetInt64(0));
        return results;
    }

    // ── Fast Travel ────────────────────────────────────────

    public void UnlockFastTravel(long poiId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO fast_travel_unlock (poi_id) VALUES ($pid);";
        cmd.Parameters.AddWithValue("$pid", poiId);
        cmd.ExecuteNonQuery();
    }

    public List<long> GetFastTravelUnlocks()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT poi_id FROM fast_travel_unlock;";
        using var r = cmd.ExecuteReader();
        var results = new List<long>();
        while (r.Read()) results.Add(r.GetInt64(0));
        return results;
    }

    // ── Map Markers ────────────────────────────────────────

    public long AddMapMarker(long regionId, int gridX, int gridY, string label, string? icon = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO map_marker (region_id, grid_x, grid_y, label, icon)
            VALUES ($rid, $gx, $gy, $lbl, $icon);
            """;
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        cmd.Parameters.AddWithValue("$lbl", label);
        cmd.Parameters.AddWithValue("$icon", (object?)icon ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public void RemoveMapMarker(long markerId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM map_marker WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", markerId);
        cmd.ExecuteNonQuery();
    }

    public List<MapMarkerRecord> GetMapMarkers(long regionId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, region_id, grid_x, grid_y, label, icon FROM map_marker WHERE region_id = $rid;";
        cmd.Parameters.AddWithValue("$rid", regionId);
        using var r = cmd.ExecuteReader();
        var results = new List<MapMarkerRecord>();
        while (r.Read())
            results.Add(new MapMarkerRecord(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.GetInt32(3),
                r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5)));
        return results;
    }

    // ── Save Meta ──────────────────────────────────────────

    public void SetCurrentRegion(string regionName)
    {
        SetMeta("current_region", regionName);
    }

    public string? GetCurrentRegion()
    {
        return GetMeta("current_region");
    }

    public void SavePlayerPosition(string regionName, float x, float y, float z)
    {
        var json = $"{{\"x\":{x},\"y\":{y},\"z\":{z}}}";
        SetMeta($"pos:{regionName}", json);
    }

    public (float X, float Y, float Z)? GetPlayerPosition(string regionName)
    {
        var json = GetMeta($"pos:{regionName}");
        if (json == null) return null;

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.GetProperty("x").GetSingle(),
                root.GetProperty("y").GetSingle(),
                root.GetProperty("z").GetSingle());
    }

    private void SetMeta(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO save_meta (key, value) VALUES ($key, $val)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$val", value);
        cmd.ExecuteNonQuery();
    }

    private string? GetMeta(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM save_meta WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    // ── Helpers ────────────────────────────────────────────

    private long LastId()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid();";
        return (long)cmd.ExecuteScalar()!;
    }

    public void Dispose() => _connection.Dispose();
}

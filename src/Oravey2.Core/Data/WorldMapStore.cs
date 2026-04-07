using System.Numerics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Oravey2.Core.World;

namespace Oravey2.Core.Data;

public sealed record ContinentRecord(long Id, string Name, string? Description, int GridWidth, int GridHeight);
public sealed record RegionRecord(long Id, long ContinentId, string Name, int GridX, int GridY, string Biome, double BaseHeight, string? Description);
public sealed record ChunkRecord(long Id, long RegionId, int GridX, int GridY, ChunkMode Mode, MapLayer Layer, byte[] TileData);
public sealed record PoiRecord(long Id, long RegionId, string Name, string Type, int GridX, int GridY, string? Description, string? Icon);

public sealed class WorldMapStore : IDisposable
{
    private readonly SqliteConnection _connection;

    public WorldMapStore(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        DatabaseInitializer.InitializeWorld(_connection);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>For testing with an already-open in-memory connection.</summary>
    internal WorldMapStore(SqliteConnection connection)
    {
        _connection = connection;
        DatabaseInitializer.InitializeWorld(_connection);
    }

    // ── Continent ──────────────────────────────────────────

    public long InsertContinent(string name, string? description, int gridWidth, int gridHeight)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO continent (name, description, grid_width, grid_height)
            VALUES ($name, $desc, $w, $h);
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$w", gridWidth);
        cmd.Parameters.AddWithValue("$h", gridHeight);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public ContinentRecord? GetContinent(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, description, grid_width, grid_height FROM continent WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new ContinentRecord(r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3), r.GetInt32(4))
            : null;
    }

    // ── Region ─────────────────────────────────────────────

    public long InsertRegion(long continentId, string name, int gridX, int gridY,
        string biome = "wasteland", double baseHeight = 0, string? description = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO region (continent_id, name, grid_x, grid_y, biome, base_height, description)
            VALUES ($cid, $name, $gx, $gy, $biome, $bh, $desc);
            """;
        cmd.Parameters.AddWithValue("$cid", continentId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        cmd.Parameters.AddWithValue("$biome", biome);
        cmd.Parameters.AddWithValue("$bh", baseHeight);
        cmd.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public RegionRecord? GetRegion(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, continent_id, name, grid_x, grid_y, biome, base_height, description FROM region WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadRegion(r) : null;
    }

    public RegionRecord? GetRegionByGrid(long continentId, int gridX, int gridY)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, continent_id, name, grid_x, grid_y, biome, base_height, description FROM region WHERE continent_id = $cid AND grid_x = $gx AND grid_y = $gy;";
        cmd.Parameters.AddWithValue("$cid", continentId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadRegion(r) : null;
    }

    // ── Chunk ──────────────────────────────────────────────

    public long InsertChunk(long regionId, int gridX, int gridY, byte[] tileData,
        ChunkMode mode = ChunkMode.Heightmap, MapLayer layer = MapLayer.Surface)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO chunk (region_id, grid_x, grid_y, mode, layer, tile_data)
            VALUES ($rid, $gx, $gy, $mode, $layer, $td);
            """;
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        cmd.Parameters.AddWithValue("$mode", (int)mode);
        cmd.Parameters.AddWithValue("$layer", (int)layer);
        cmd.Parameters.AddWithValue("$td", tileData);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public ChunkRecord? GetChunk(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, region_id, grid_x, grid_y, mode, layer, tile_data FROM chunk WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadChunk(r) : null;
    }

    public ChunkRecord? GetChunkByGrid(long regionId, int gridX, int gridY)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, region_id, grid_x, grid_y, mode, layer, tile_data FROM chunk WHERE region_id = $rid AND grid_x = $gx AND grid_y = $gy;";
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadChunk(r) : null;
    }

    // ── Chunk Layer ────────────────────────────────────────

    public long InsertChunkLayer(long chunkId, MapLayer layer, byte[] tileData)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO chunk_layer (chunk_id, layer, tile_data)
            VALUES ($cid, $layer, $td);
            """;
        cmd.Parameters.AddWithValue("$cid", chunkId);
        cmd.Parameters.AddWithValue("$layer", (int)layer);
        cmd.Parameters.AddWithValue("$td", tileData);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public List<(long Id, MapLayer Layer, byte[] TileData)> GetChunkLayers(long chunkId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, layer, tile_data FROM chunk_layer WHERE chunk_id = $cid;";
        cmd.Parameters.AddWithValue("$cid", chunkId);
        using var r = cmd.ExecuteReader();
        var results = new List<(long, MapLayer, byte[])>();
        while (r.Read())
            results.Add((r.GetInt64(0), (MapLayer)r.GetInt32(1), (byte[])r[2]));
        return results;
    }

    // ── Entity Spawn ───────────────────────────────────────

    public long InsertEntitySpawn(long chunkId, EntitySpawnInfo spawn)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entity_spawn (chunk_id, prefab_id, local_x, local_z, rotation_y, faction, level, dialogue_id, loot_table, persistent, condition_flag)
            VALUES ($cid, $pid, $lx, $lz, $ry, $fac, $lvl, $did, $lt, $per, $cf);
            """;
        cmd.Parameters.AddWithValue("$cid", chunkId);
        cmd.Parameters.AddWithValue("$pid", spawn.PrefabId);
        cmd.Parameters.AddWithValue("$lx", spawn.LocalX);
        cmd.Parameters.AddWithValue("$lz", spawn.LocalZ);
        cmd.Parameters.AddWithValue("$ry", spawn.RotationY);
        cmd.Parameters.AddWithValue("$fac", (object?)spawn.Faction ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lvl", spawn.Level.HasValue ? spawn.Level.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$did", (object?)spawn.DialogueId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lt", (object?)spawn.LootTable ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$per", spawn.Persistent ? 1 : 0);
        cmd.Parameters.AddWithValue("$cf", (object?)spawn.ConditionFlag ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public List<EntitySpawnInfo> GetEntitySpawns(long chunkId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT prefab_id, local_x, local_z, rotation_y, faction, level, dialogue_id, loot_table, persistent, condition_flag FROM entity_spawn WHERE chunk_id = $cid;";
        cmd.Parameters.AddWithValue("$cid", chunkId);
        using var r = cmd.ExecuteReader();
        var results = new List<EntitySpawnInfo>();
        while (r.Read())
        {
            results.Add(new EntitySpawnInfo(
                PrefabId: r.GetString(0),
                LocalX: r.GetFloat(1),
                LocalZ: r.GetFloat(2),
                RotationY: r.GetFloat(3),
                Faction: r.IsDBNull(4) ? null : r.GetString(4),
                Level: r.IsDBNull(5) ? null : r.GetInt32(5),
                DialogueId: r.IsDBNull(6) ? null : r.GetString(6),
                LootTable: r.IsDBNull(7) ? null : r.GetString(7),
                Persistent: r.GetInt32(8) != 0,
                ConditionFlag: r.IsDBNull(9) ? null : r.GetString(9)
            ));
        }
        return results;
    }

    // ── Linear Feature ─────────────────────────────────────

    public long InsertLinearFeature(long regionId, LinearFeature feature)
    {
        var nodesJson = JsonSerializer.Serialize(
            feature.Nodes.Select(n => new { x = n.Position.X, y = n.Position.Y, h = n.OverrideHeight }));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO linear_feature (region_id, type, style, width, nodes_json)
            VALUES ($rid, $type, $style, $width, $nodes);
            """;
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$type", (int)feature.Type);
        cmd.Parameters.AddWithValue("$style", feature.Style);
        cmd.Parameters.AddWithValue("$width", feature.Width);
        cmd.Parameters.AddWithValue("$nodes", nodesJson);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public List<LinearFeature> GetLinearFeatures(long regionId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT type, style, width, nodes_json FROM linear_feature WHERE region_id = $rid;";
        cmd.Parameters.AddWithValue("$rid", regionId);
        using var r = cmd.ExecuteReader();
        var results = new List<LinearFeature>();
        while (r.Read())
        {
            var type = (LinearFeatureType)r.GetInt32(0);
            var style = r.GetString(1);
            var width = (float)r.GetDouble(2);
            var nodesJson = r.GetString(3);
            var rawNodes = JsonSerializer.Deserialize<JsonElement[]>(nodesJson) ?? [];
            var nodes = rawNodes.Select(n => new LinearFeatureNode(
                new Vector2(n.GetProperty("x").GetSingle(), n.GetProperty("y").GetSingle()),
                n.TryGetProperty("h", out var h) && h.ValueKind != JsonValueKind.Null ? h.GetSingle() : null
            )).ToList();
            results.Add(new LinearFeature(type, style, width, nodes));
        }
        return results;
    }

    // ── POI ────────────────────────────────────────────────

    public long InsertPoi(long regionId, string name, string type, int gridX, int gridY,
        string? description = null, string? icon = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO poi (region_id, name, type, grid_x, grid_y, description, icon)
            VALUES ($rid, $name, $type, $gx, $gy, $desc, $icon);
            """;
        cmd.Parameters.AddWithValue("$rid", regionId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$gx", gridX);
        cmd.Parameters.AddWithValue("$gy", gridY);
        cmd.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$icon", (object?)icon ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public List<PoiRecord> GetPois(long regionId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, region_id, name, type, grid_x, grid_y, description, icon FROM poi WHERE region_id = $rid;";
        cmd.Parameters.AddWithValue("$rid", regionId);
        using var r = cmd.ExecuteReader();
        var results = new List<PoiRecord>();
        while (r.Read())
            results.Add(new PoiRecord(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3),
                r.GetInt32(4), r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)));
        return results;
    }

    // ── Terrain Modifier ───────────────────────────────────

    public long InsertTerrainModifier(long chunkId, TerrainModifier modifier)
    {
        var type = modifier.GetType().Name;
        var json = JsonSerializer.Serialize<object>(modifier);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO terrain_modifier (chunk_id, type, data_json)
            VALUES ($cid, $type, $json);
            """;
        cmd.Parameters.AddWithValue("$cid", chunkId);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.ExecuteNonQuery();
        return LastId();
    }

    public List<TerrainModifier> GetTerrainModifiers(long chunkId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT type, data_json FROM terrain_modifier WHERE chunk_id = $cid;";
        cmd.Parameters.AddWithValue("$cid", chunkId);
        using var r = cmd.ExecuteReader();
        var results = new List<TerrainModifier>();
        while (r.Read())
        {
            var type = r.GetString(0);
            var json = r.GetString(1);
            TerrainModifier? mod = type switch
            {
                nameof(FlattenStrip) => JsonSerializer.Deserialize<FlattenStrip>(json),
                nameof(ChannelCut) => JsonSerializer.Deserialize<ChannelCut>(json),
                nameof(LevelRect) => JsonSerializer.Deserialize<LevelRect>(json),
                nameof(Crater) => JsonSerializer.Deserialize<Crater>(json),
                _ => null
            };
            if (mod is not null) results.Add(mod);
        }
        return results;
    }

    // ── World Meta ─────────────────────────────────────────

    public string? GetMeta(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM world_meta WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public string GetOrSetMeta(string key, string defaultValue)
    {
        var existing = GetMeta(key);
        if (existing is not null) return existing;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO world_meta (key, value) VALUES ($key, $val);";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$val", defaultValue);
        cmd.ExecuteNonQuery();
        return defaultValue;
    }

    // ── Helpers ────────────────────────────────────────────

    private long LastId()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid();";
        return (long)cmd.ExecuteScalar()!;
    }

    private static RegionRecord ReadRegion(SqliteDataReader r) =>
        new(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4),
            r.GetString(5), r.GetDouble(6), r.IsDBNull(7) ? null : r.GetString(7));

    private static ChunkRecord ReadChunk(SqliteDataReader r) =>
        new(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.GetInt32(3),
            (ChunkMode)r.GetInt32(4), (MapLayer)r.GetInt32(5), (byte[])r[6]);

    public void Dispose() => _connection.Dispose();
}

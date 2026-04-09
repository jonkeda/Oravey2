using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class SparseRegionGenerator
{
    public List<ChunkResult> Generate(
        int regionSeed,
        RegionTemplate region,
        List<LinearFeatureData> majorRoads,
        int startChunkX, int startChunkY,
        int chunkCountX, int chunkCountY)
    {
        var rng = new Random(regionSeed);
        var wilderness = new WildernessChunkGenerator();
        var chunks = new List<ChunkResult>();

        for (int cx = startChunkX; cx < startChunkX + chunkCountX; cx++)
        {
            for (int cy = startChunkY; cy < startChunkY + chunkCountY; cy++)
            {
                var chunk = wilderness.Generate(cx, cy, regionSeed, region);
                chunks.Add(chunk);
            }
        }

        // Place 1–3 procedural outposts at road intersections or random positions
        int outpostCount = rng.Next(1, 4);
        var outpostPositions = FindOutpostPositions(majorRoads, rng, outpostCount,
            startChunkX, startChunkY, chunkCountX, chunkCountY);

        foreach (var pos in outpostPositions)
        {
            int cx = (int)Math.Floor(pos.X / ChunkData.Size);
            int cy = (int)Math.Floor(pos.Y / ChunkData.Size);
            int localX = (int)(pos.X - cx * ChunkData.Size);
            int localZ = (int)(pos.Y - cy * ChunkData.Size);

            var chunk = chunks.FirstOrDefault(c => c.ChunkX == cx && c.ChunkY == cy);
            if (chunk == null) continue;

            string prefab = rng.Next(3) switch
            {
                0 => "outpost_gas_station",
                1 => "outpost_checkpoint",
                _ => "outpost_radio_tower"
            };

            chunk.Entities.Add(new EntitySpawnInfo(
                PrefabId: prefab,
                LocalX: Math.Clamp(localX, 1, ChunkData.Size - 2),
                LocalZ: Math.Clamp(localZ, 1, ChunkData.Size - 2),
                RotationY: rng.Next(4) * 90f,
                Faction: "neutral",
                Level: rng.Next(1, 5)));
        }

        return chunks;
    }

    private static List<Vector2> FindOutpostPositions(
        List<LinearFeatureData> roads, Random rng, int count,
        int startCX, int startCY, int countX, int countY)
    {
        var positions = new List<Vector2>();
        float minX = startCX * ChunkData.Size;
        float minZ = startCY * ChunkData.Size;
        float maxX = (startCX + countX) * ChunkData.Size;
        float maxZ = (startCY + countY) * ChunkData.Size;

        // Try to place at road midpoints first
        foreach (var road in roads)
        {
            if (positions.Count >= count) break;
            if (road.Nodes.Length < 2) continue;
            var mid = road.Nodes[road.Nodes.Length / 2];
            if (mid.X >= minX && mid.X < maxX && mid.Y >= minZ && mid.Y < maxZ)
                positions.Add(mid);
        }

        // Fill remainder with random positions
        while (positions.Count < count)
        {
            positions.Add(new Vector2(
                minX + (float)(rng.NextDouble() * (maxX - minX)),
                minZ + (float)(rng.NextDouble() * (maxZ - minZ))));
        }

        return positions;
    }
}

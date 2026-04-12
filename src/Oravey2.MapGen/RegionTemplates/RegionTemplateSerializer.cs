using System.Numerics;
using Oravey2.Core.World;

namespace Oravey2.MapGen.RegionTemplates;

public static class RegionTemplateSerializer
{
    private static readonly byte[] Magic = "ORRT"u8.ToArray();
    private const int Version = 1;

    public static async Task SaveAsync(RegionTemplate template, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        using var bw = new BinaryWriter(stream);

        bw.Write(Magic);
        bw.Write(Version);

        bw.Write(template.Name);
        bw.Write(template.GridOriginLat);
        bw.Write(template.GridOriginLon);
        bw.Write(template.GridCellSizeMetres);

        // Elevation grid
        int rows = template.ElevationGrid.GetLength(0);
        int cols = template.ElevationGrid.GetLength(1);
        bw.Write(rows);
        bw.Write(cols);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                bw.Write(template.ElevationGrid[r, c]);

        // Towns
        bw.Write(template.Towns.Count);
        foreach (var t in template.Towns)
        {
            bw.Write(t.Name);
            bw.Write(t.Latitude);
            bw.Write(t.Longitude);
            bw.Write(t.Population);
            bw.Write(t.GamePosition.X);
            bw.Write(t.GamePosition.Y);
            bw.Write((int)t.Category);
            bool hasBoundary = t.BoundaryPolygon is { Length: > 0 };
            bw.Write(hasBoundary);
            if (hasBoundary)
            {
                bw.Write(t.BoundaryPolygon!.Length);
                foreach (var p in t.BoundaryPolygon)
                {
                    bw.Write(p.X);
                    bw.Write(p.Y);
                }
            }
        }

        // Roads
        bw.Write(template.Roads.Count);
        foreach (var r in template.Roads)
        {
            bw.Write((int)r.RoadClass);
            WriteVector2Array(bw, r.Nodes);
        }

        // Water
        bw.Write(template.WaterBodies.Count);
        foreach (var w in template.WaterBodies)
        {
            bw.Write((int)w.Type);
            WriteVector2Array(bw, w.Geometry);
        }

        // Railways
        bw.Write(template.Railways.Count);
        foreach (var r in template.Railways)
            WriteVector2Array(bw, r.Nodes);

        // Land use
        bw.Write(template.LandUseZones.Count);
        foreach (var z in template.LandUseZones)
        {
            bw.Write((int)z.Type);
            WriteVector2Array(bw, z.Polygon);
        }

        await stream.FlushAsync();
    }

    public static async Task<RegionTemplate?> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            using var br = new BinaryReader(stream);

            // Validate magic
            var magic = br.ReadBytes(4);
            if (magic.Length < 4 ||
                magic[0] != Magic[0] || magic[1] != Magic[1] ||
                magic[2] != Magic[2] || magic[3] != Magic[3])
                return null;

            int version = br.ReadInt32();
            if (version != Version)
                return null;

            string name = br.ReadString();
            double originLat = br.ReadDouble();
            double originLon = br.ReadDouble();
            double cellSize = br.ReadDouble();

            // Elevation grid
            int rows = br.ReadInt32();
            int cols = br.ReadInt32();
            var grid = new float[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = br.ReadSingle();

            // Towns
            int townCount = br.ReadInt32();
            var towns = new List<TownEntry>(townCount);
            for (int i = 0; i < townCount; i++)
            {
                string tName = br.ReadString();
                double lat = br.ReadDouble();
                double lon = br.ReadDouble();
                int pop = br.ReadInt32();
                float gx = br.ReadSingle();
                float gy = br.ReadSingle();
                var cat = (TownCategory)br.ReadInt32();
                bool hasBoundary = br.ReadBoolean();
                Vector2[]? boundary = null;
                if (hasBoundary)
                    boundary = ReadVector2Array(br);
                towns.Add(new TownEntry(tName, lat, lon, pop, new Vector2(gx, gy), cat, boundary));
            }

            // Roads
            int roadCount = br.ReadInt32();
            var roads = new List<RoadSegment>(roadCount);
            for (int i = 0; i < roadCount; i++)
            {
                var cls = (LinearFeatureType)br.ReadInt32();
                var nodes = ReadVector2Array(br);
                roads.Add(new RoadSegment(cls, nodes));
            }

            // Water
            int waterCount = br.ReadInt32();
            var water = new List<WaterBody>(waterCount);
            for (int i = 0; i < waterCount; i++)
            {
                var type = (WaterType)br.ReadInt32();
                var geo = ReadVector2Array(br);
                water.Add(new WaterBody(type, geo));
            }

            // Railways
            int railCount = br.ReadInt32();
            var railways = new List<RailwaySegment>(railCount);
            for (int i = 0; i < railCount; i++)
                railways.Add(new RailwaySegment(ReadVector2Array(br)));

            // Land use
            int landCount = br.ReadInt32();
            var landUse = new List<LandUseZone>(landCount);
            for (int i = 0; i < landCount; i++)
            {
                var type = (LandUseType)br.ReadInt32();
                var poly = ReadVector2Array(br);
                landUse.Add(new LandUseZone(type, poly));
            }

            return new RegionTemplate
            {
                Name = name,
                ElevationGrid = grid,
                GridOriginLat = originLat,
                GridOriginLon = originLon,
                GridCellSizeMetres = cellSize,
                Towns = towns,
                Roads = roads,
                WaterBodies = water,
                Railways = railways,
                LandUseZones = landUse,
            };
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void WriteVector2Array(BinaryWriter bw, Vector2[] arr)
    {
        bw.Write(arr.Length);
        foreach (var v in arr)
        {
            bw.Write(v.X);
            bw.Write(v.Y);
        }
    }

    private static Vector2[] ReadVector2Array(BinaryReader br)
    {
        int count = br.ReadInt32();
        var arr = new Vector2[count];
        for (int i = 0; i < count; i++)
            arr[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
        return arr;
    }
}

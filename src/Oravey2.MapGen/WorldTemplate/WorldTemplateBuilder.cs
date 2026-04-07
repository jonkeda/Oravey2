using System.Numerics;

namespace Oravey2.MapGen.WorldTemplate;

/// <summary>
/// Combines elevation data and OSM data into a complete <see cref="WorldTemplate"/>.
/// Serialises/deserialises to a compact binary format.
/// </summary>
public class WorldTemplateBuilder
{
    private readonly GeoMapper _geoMapper;

    public WorldTemplateBuilder(GeoMapper geoMapper)
    {
        _geoMapper = geoMapper;
    }

    public WorldTemplate Build(string name, float[,] elevationGrid, OsmExtract osmData,
        double gridOriginLat, double gridOriginLon, double gridCellSizeMetres)
    {
        var region = new RegionTemplate
        {
            Name = name,
            ElevationGrid = elevationGrid,
            GridOriginLat = gridOriginLat,
            GridOriginLon = gridOriginLon,
            GridCellSizeMetres = gridCellSizeMetres,
            Towns = osmData.Towns,
            Roads = osmData.Roads,
            WaterBodies = osmData.WaterBodies,
            Railways = osmData.Railways,
            LandUseZones = osmData.LandUseZones
        };

        return new WorldTemplate
        {
            Name = name,
            OriginLatitude = _geoMapper.OriginLatitude,
            OriginLongitude = _geoMapper.OriginLongitude,
            Regions = [region]
        };
    }

    public static void Serialize(WorldTemplate template, string outputPath)
    {
        using var stream = File.Create(outputPath);
        Serialize(template, stream);
    }

    public static void Serialize(WorldTemplate template, Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Header
        writer.Write("OWTP"u8); // Magic bytes: Oravey2 World Template
        writer.Write(WorldTemplate.FormatVersion);
        writer.Write(template.Name);
        writer.Write(template.OriginLatitude);
        writer.Write(template.OriginLongitude);

        // Continent outlines
        writer.Write(template.ContinentOutlines.Count);
        foreach (var outline in template.ContinentOutlines)
            WriteVector2Array(writer, outline);

        // Regions
        writer.Write(template.Regions.Count);
        foreach (var region in template.Regions)
            WriteRegion(writer, region);
    }

    public static WorldTemplate Deserialize(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Deserialize(stream);
    }

    public static WorldTemplate Deserialize(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Header
        Span<byte> magic = stackalloc byte[4];
        reader.Read(magic);
        if (magic[0] != (byte)'O' || magic[1] != (byte)'W' || magic[2] != (byte)'T' || magic[3] != (byte)'P')
            throw new FormatException("Invalid WorldTemplate file: bad magic bytes.");

        int version = reader.ReadInt32();
        if (version != WorldTemplate.FormatVersion)
            throw new FormatException($"Unsupported WorldTemplate version {version}. Expected {WorldTemplate.FormatVersion}.");

        string name = reader.ReadString();
        double originLat = reader.ReadDouble();
        double originLon = reader.ReadDouble();

        // Continent outlines
        int outlineCount = reader.ReadInt32();
        var outlines = new List<Vector2[]>(outlineCount);
        for (int i = 0; i < outlineCount; i++)
            outlines.Add(ReadVector2Array(reader));

        // Regions
        int regionCount = reader.ReadInt32();
        var regions = new List<RegionTemplate>(regionCount);
        for (int i = 0; i < regionCount; i++)
            regions.Add(ReadRegion(reader));

        return new WorldTemplate
        {
            Name = name,
            OriginLatitude = originLat,
            OriginLongitude = originLon,
            ContinentOutlines = outlines,
            Regions = regions
        };
    }

    private static void WriteRegion(BinaryWriter writer, RegionTemplate region)
    {
        writer.Write(region.Name);
        writer.Write(region.GridOriginLat);
        writer.Write(region.GridOriginLon);
        writer.Write(region.GridCellSizeMetres);

        // Elevation grid
        int rows = region.ElevationGrid.GetLength(0);
        int cols = region.ElevationGrid.GetLength(1);
        writer.Write(rows);
        writer.Write(cols);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                writer.Write(region.ElevationGrid[r, c]);

        // Towns
        writer.Write(region.Towns.Count);
        foreach (var town in region.Towns)
        {
            writer.Write(town.Name);
            writer.Write(town.Latitude);
            writer.Write(town.Longitude);
            writer.Write(town.Population);
            writer.Write(town.GamePosition.X);
            writer.Write(town.GamePosition.Y);
            writer.Write((byte)town.Category);
            writer.Write(town.BoundaryPolygon != null);
            if (town.BoundaryPolygon != null)
                WriteVector2Array(writer, town.BoundaryPolygon);
        }

        // Roads
        writer.Write(region.Roads.Count);
        foreach (var road in region.Roads)
        {
            writer.Write((byte)road.RoadClass);
            WriteVector2Array(writer, road.Nodes);
        }

        // Water bodies
        writer.Write(region.WaterBodies.Count);
        foreach (var water in region.WaterBodies)
        {
            writer.Write((byte)water.Type);
            WriteVector2Array(writer, water.Geometry);
        }

        // Railways
        writer.Write(region.Railways.Count);
        foreach (var rail in region.Railways)
            WriteVector2Array(writer, rail.Nodes);

        // Land use zones
        writer.Write(region.LandUseZones.Count);
        foreach (var zone in region.LandUseZones)
        {
            writer.Write((byte)zone.Type);
            WriteVector2Array(writer, zone.Polygon);
        }
    }

    private static RegionTemplate ReadRegion(BinaryReader reader)
    {
        string name = reader.ReadString();
        double gridOriginLat = reader.ReadDouble();
        double gridOriginLon = reader.ReadDouble();
        double gridCellSize = reader.ReadDouble();

        // Elevation grid
        int rows = reader.ReadInt32();
        int cols = reader.ReadInt32();
        var grid = new float[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                grid[r, c] = reader.ReadSingle();

        // Towns
        int townCount = reader.ReadInt32();
        var towns = new List<TownEntry>(townCount);
        for (int i = 0; i < townCount; i++)
        {
            string tName = reader.ReadString();
            double lat = reader.ReadDouble();
            double lon = reader.ReadDouble();
            int pop = reader.ReadInt32();
            float gx = reader.ReadSingle();
            float gy = reader.ReadSingle();
            var cat = (TownCategory)reader.ReadByte();
            bool hasBoundary = reader.ReadBoolean();
            Vector2[]? boundary = hasBoundary ? ReadVector2Array(reader) : null;
            towns.Add(new TownEntry(tName, lat, lon, pop, new Vector2(gx, gy), cat, boundary));
        }

        // Roads
        int roadCount = reader.ReadInt32();
        var roads = new List<RoadSegment>(roadCount);
        for (int i = 0; i < roadCount; i++)
        {
            var cls = (RoadClass)reader.ReadByte();
            roads.Add(new RoadSegment(cls, ReadVector2Array(reader)));
        }

        // Water bodies
        int waterCount = reader.ReadInt32();
        var water = new List<WaterBody>(waterCount);
        for (int i = 0; i < waterCount; i++)
        {
            var type = (WaterType)reader.ReadByte();
            water.Add(new WaterBody(type, ReadVector2Array(reader)));
        }

        // Railways
        int railCount = reader.ReadInt32();
        var rails = new List<RailwaySegment>(railCount);
        for (int i = 0; i < railCount; i++)
            rails.Add(new RailwaySegment(ReadVector2Array(reader)));

        // Land use zones
        int luCount = reader.ReadInt32();
        var landUse = new List<LandUseZone>(luCount);
        for (int i = 0; i < luCount; i++)
        {
            var type = (LandUseType)reader.ReadByte();
            landUse.Add(new LandUseZone(type, ReadVector2Array(reader)));
        }

        return new RegionTemplate
        {
            Name = name,
            ElevationGrid = grid,
            GridOriginLat = gridOriginLat,
            GridOriginLon = gridOriginLon,
            GridCellSizeMetres = gridCellSize,
            Towns = towns,
            Roads = roads,
            WaterBodies = water,
            Railways = rails,
            LandUseZones = landUse
        };
    }

    private static void WriteVector2Array(BinaryWriter writer, Vector2[] arr)
    {
        writer.Write(arr.Length);
        foreach (var v in arr)
        {
            writer.Write(v.X);
            writer.Write(v.Y);
        }
    }

    private static Vector2[] ReadVector2Array(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var arr = new Vector2[count];
        for (int i = 0; i < count; i++)
            arr[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        return arr;
    }
}

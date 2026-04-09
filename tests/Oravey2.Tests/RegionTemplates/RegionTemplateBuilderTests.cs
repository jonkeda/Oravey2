using System.IO.Compression;
using System.Numerics;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.RegionTemplates;

public class RegionTemplateBuilderTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrips()
    {
        var geoMapper = new GeoMapper();
        var builder = new RegionTemplateBuilder(geoMapper);

        var osmData = new OsmExtract(
            Towns:
            [
                new TownEntry("TestTown", 52.51, 4.96, 5000, new Vector2(100, 200), TownCategory.Town)
            ],
            Roads:
            [
                new RoadSegment(RoadClass.Primary, [new Vector2(0, 0), new Vector2(100, 100)])
            ],
            WaterBodies:
            [
                new WaterBody(WaterType.Lake, [new Vector2(10, 10), new Vector2(20, 20), new Vector2(10, 20)])
            ],
            Railways:
            [
                new RailwaySegment([new Vector2(0, 0), new Vector2(50, 50)])
            ],
            LandUseZones:
            [
                new LandUseZone(LandUseType.Forest, [new Vector2(5, 5), new Vector2(15, 15), new Vector2(5, 15)])
            ]
        );

        var elevationGrid = new float[3, 3]
        {
            { 1f, 2f, 3f },
            { 4f, 5f, 6f },
            { 7f, 8f, 9f }
        };

        var template = builder.Build("TestRegion", elevationGrid, osmData, 52.50, 4.95, 30.0);

        using var stream = new MemoryStream();
        RegionTemplateBuilder.Serialize(template, stream);

        stream.Position = 0;
        var deserialized = RegionTemplateBuilder.Deserialize(stream);

        Assert.Equal("TestRegion", deserialized.Name);
        Assert.Equal(52.50, deserialized.OriginLatitude);
        Assert.Equal(4.95, deserialized.OriginLongitude);
        Assert.Single(deserialized.Regions);

        var region = deserialized.Regions[0];
        Assert.Equal("TestRegion", region.Name);
        Assert.Equal(3, region.ElevationGrid.GetLength(0));
        Assert.Equal(3, region.ElevationGrid.GetLength(1));
        Assert.Equal(5f, region.ElevationGrid[1, 1]);

        Assert.Single(region.Towns);
        Assert.Equal("TestTown", region.Towns[0].Name);
        Assert.Equal(TownCategory.Town, region.Towns[0].Category);

        Assert.Single(region.Roads);
        Assert.Equal(RoadClass.Primary, region.Roads[0].RoadClass);

        Assert.Single(region.WaterBodies);
        Assert.Equal(WaterType.Lake, region.WaterBodies[0].Type);

        Assert.Single(region.Railways);
        Assert.Single(region.LandUseZones);
        Assert.Equal(LandUseType.Forest, region.LandUseZones[0].Type);
    }

    [Fact]
    public void Deserialize_V1Uncompressed_StillReadable()
    {
        // Write a v1-format file (uncompressed) manually
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("ORTP"u8);
            writer.Write(1); // version 1

            writer.Write("V1Region");       // Name
            writer.Write(52.50);            // OriginLatitude
            writer.Write(4.95);             // OriginLongitude
            writer.Write(0);                // ContinentOutlineCount
            writer.Write(1);                // RegionCount

            // Region
            writer.Write("V1Region");       // Region name
            writer.Write(52.50);            // GridOriginLat
            writer.Write(4.95);             // GridOriginLon
            writer.Write(30.0);             // GridCellSizeMetres
            writer.Write(2);                // rows
            writer.Write(2);                // cols
            writer.Write(1f); writer.Write(2f);
            writer.Write(3f); writer.Write(4f);
            writer.Write(0);                // TownCount
            writer.Write(0);                // RoadCount
            writer.Write(0);                // WaterCount
            writer.Write(0);                // RailwayCount
            writer.Write(0);                // LandUseCount
        }

        stream.Position = 0;
        var template = RegionTemplateBuilder.Deserialize(stream);

        Assert.Equal("V1Region", template.Name);
        Assert.Equal(52.50, template.OriginLatitude);
        Assert.Single(template.Regions);
        Assert.Equal(2, template.Regions[0].ElevationGrid.GetLength(0));
        Assert.Equal(4f, template.Regions[0].ElevationGrid[1, 1]);
    }

    [Fact]
    public void Serialize_V2_StartsWithMagicAndVersion()
    {
        var template = BuildSmallTemplate();

        using var stream = new MemoryStream();
        RegionTemplateBuilder.Serialize(template, stream);

        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        // Uncompressed header
        var magic = reader.ReadBytes(4);
        Assert.Equal((byte)'O', magic[0]);
        Assert.Equal((byte)'R', magic[1]);
        Assert.Equal((byte)'T', magic[2]);
        Assert.Equal((byte)'P', magic[3]);

        int version = reader.ReadInt32();
        Assert.Equal(2, version);

        // Rest should be GZip (starts with 0x1F 0x8B)
        byte gz1 = reader.ReadByte();
        byte gz2 = reader.ReadByte();
        Assert.Equal(0x1F, gz1);
        Assert.Equal(0x8B, gz2);
    }

    [Fact]
    public void Serialize_V2Compressed_SmallerThanV1()
    {
        // Build a template with a large-ish elevation grid to see compression
        var geoMapper = new GeoMapper();
        var builder = new RegionTemplateBuilder(geoMapper);

        var grid = new float[100, 100];
        for (int r = 0; r < 100; r++)
            for (int c = 0; c < 100; c++)
                grid[r, c] = r * 0.5f + c * 0.1f; // spatial locality → compresses well

        var osmData = new OsmExtract([], [], [], [], []);
        var template = builder.Build("CompressTest", grid, osmData, 52.50, 4.95, 30.0);

        // V2 (compressed)
        using var v2Stream = new MemoryStream();
        RegionTemplateBuilder.Serialize(template, v2Stream);

        // V1-equivalent (uncompressed payload size)
        using var v1Stream = new MemoryStream();
        using (var w = new BinaryWriter(v1Stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write("ORTP"u8);
            w.Write(1);
            w.Write(template.Name);
            w.Write(template.OriginLatitude);
            w.Write(template.OriginLongitude);
            w.Write(0); // outlines
            w.Write(1); // regions
            w.Write(template.Name);
            w.Write(52.50); w.Write(4.95); w.Write(30.0);
            w.Write(100); w.Write(100);
            for (int r = 0; r < 100; r++)
                for (int c = 0; c < 100; c++)
                    w.Write(grid[r, c]);
            w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        }

        Assert.True(v2Stream.Length < v1Stream.Length,
            $"V2 ({v2Stream.Length} bytes) should be smaller than V1 ({v1Stream.Length} bytes)");
    }

    private static RegionTemplateFile BuildSmallTemplate()
    {
        var geoMapper = new GeoMapper();
        var builder = new RegionTemplateBuilder(geoMapper);
        var osmData = new OsmExtract([], [], [], [], []);
        return builder.Build("Small", new float[2, 2] { { 1f, 2f }, { 3f, 4f } }, osmData, 52.50, 4.95, 30.0);
    }
}

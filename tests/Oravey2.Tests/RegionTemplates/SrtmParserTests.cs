using System.IO.Compression;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.RegionTemplates;

public class SrtmParserTests
{
    [Fact]
    public void ParseHgtFile_ProducesGrid()
    {
        // Create a small 3-arcsecond test HGT (1201 × 1201 = 1,442,401 samples, big-endian int16)
        int dim = 1201;
        var bytes = new byte[dim * dim * 2];
        var rng = new Random(42);

        for (int i = 0; i < bytes.Length; i += 2)
        {
            short elevation = (short)rng.Next(0, 50); // 0–49 m, flat Dutch terrain
            bytes[i] = (byte)(elevation >> 8);
            bytes[i + 1] = (byte)(elevation & 0xFF);
        }

        var geoMapper = new GeoMapper();
        var parser = new SrtmParser(geoMapper);
        var grid = parser.ParseHgtFile(bytes);

        Assert.Equal(dim, grid.GetLength(0));
        Assert.Equal(dim, grid.GetLength(1));
        Assert.True(grid[0, 0] >= 0 && grid[0, 0] < 50);
    }

    [Fact]
    public void VoidValues_AreFilled()
    {
        // Create small 1201×1201 grid with void cells in the middle
        int dim = 1201;
        var bytes = new byte[dim * dim * 2];

        // Fill everything with elevation 10
        for (int i = 0; i < bytes.Length; i += 2)
        {
            short elevation = 10;
            bytes[i] = (byte)(elevation >> 8);
            bytes[i + 1] = (byte)(elevation & 0xFF);
        }

        // Set a cell to void (-32768 = 0x8000)
        int voidRow = 600;
        int voidCol = 600;
        int offset = (voidRow * dim + voidCol) * 2;
        bytes[offset] = 0x80;
        bytes[offset + 1] = 0x00;

        var geoMapper = new GeoMapper();
        var parser = new SrtmParser(geoMapper);
        var grid = parser.ParseHgtFile(bytes);

        // Void cell should be filled with neighbour average (10)
        Assert.False(float.IsNaN(grid[voidRow, voidCol]));
        Assert.Equal(10f, grid[voidRow, voidCol], 0.1f);
    }

    [Fact]
    public void ParseHgtGz_ProducesSameGrid()
    {
        int dim = 1201;
        var bytes = new byte[dim * dim * 2];
        var rng = new Random(42);
        for (int i = 0; i < bytes.Length; i += 2)
        {
            short elevation = (short)rng.Next(0, 50);
            bytes[i] = (byte)(elevation >> 8);
            bytes[i + 1] = (byte)(elevation & 0xFF);
        }

        var geoMapper = new GeoMapper();
        var parser = new SrtmParser(geoMapper);
        var gridFromRaw = parser.ParseHgtFile(bytes);

        // Write gzipped to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"srtm_test_{Guid.NewGuid()}.hgt.gz");
        try
        {
            using (var output = File.Create(tempPath))
            using (var gz = new GZipStream(output, CompressionLevel.Optimal))
                gz.Write(bytes);

            var gridFromGz = parser.ParseHgtFile(tempPath);

            Assert.Equal(gridFromRaw.GetLength(0), gridFromGz.GetLength(0));
            Assert.Equal(gridFromRaw.GetLength(1), gridFromGz.GetLength(1));
            for (int r = 0; r < dim; r++)
                for (int c = 0; c < dim; c++)
                    Assert.Equal(gridFromRaw[r, c], gridFromGz[r, c]);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}

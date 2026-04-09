using System.IO.Compression;

namespace Oravey2.MapGen.RegionTemplates;

/// <summary>
/// Parses NASA SRTM HGT elevation files (1-arcsecond, ~30 m resolution).
/// Each .hgt file covers 1° × 1° and contains a grid of 3601 × 3601 signed 16-bit big-endian values.
/// </summary>
public class SrtmParser
{
    private const int Srtm1Dimension = 3601; // 1-arcsecond: 3601 × 3601
    private const int Srtm3Dimension = 1201; // 3-arcsecond: 1201 × 1201
    private const short VoidValue = -32768;

    private readonly GeoMapper _geoMapper;

    public SrtmParser(GeoMapper geoMapper)
    {
        _geoMapper = geoMapper;
    }

    public float[,] ParseHgtFile(string filePath)
    {
        byte[] bytes;
        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var fileStream = File.OpenRead(filePath);
            using var gzStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gzStream.CopyTo(ms);
            bytes = ms.ToArray();
        }
        else
        {
            bytes = File.ReadAllBytes(filePath);
        }

        return ParseHgtFile(bytes);
    }

    public float[,] ParseHgtFile(byte[] bytes)
    {
        int dimension = InferDimension(bytes.Length);
        var grid = new float[dimension, dimension];

        int offset = 0;
        for (int row = 0; row < dimension; row++)
        {
            for (int col = 0; col < dimension; col++)
            {
                short value = (short)((bytes[offset] << 8) | bytes[offset + 1]);
                grid[row, col] = value == VoidValue ? float.NaN : value;
                offset += 2;
            }
        }

        FillVoids(grid, dimension);
        return grid;
    }

    private static int InferDimension(int byteCount)
    {
        int sampleCount = byteCount / 2;
        if (sampleCount == Srtm1Dimension * Srtm1Dimension) return Srtm1Dimension;
        if (sampleCount == Srtm3Dimension * Srtm3Dimension) return Srtm3Dimension;
        throw new FormatException(
            $"HGT file has {byteCount} bytes ({sampleCount} samples). " +
            $"Expected {Srtm1Dimension * Srtm1Dimension} (1-arcsecond) or {Srtm3Dimension * Srtm3Dimension} (3-arcsecond).");
    }

    private static void FillVoids(float[,] grid, int dimension)
    {
        for (int row = 0; row < dimension; row++)
        {
            for (int col = 0; col < dimension; col++)
            {
                if (!float.IsNaN(grid[row, col])) continue;

                float sum = 0;
                int count = 0;

                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = row + dr;
                        int nc = col + dc;
                        if (nr < 0 || nr >= dimension || nc < 0 || nc >= dimension) continue;
                        if (float.IsNaN(grid[nr, nc])) continue;
                        sum += grid[nr, nc];
                        count++;
                    }
                }

                grid[row, col] = count > 0 ? sum / count : 0f;
            }
        }
    }
}

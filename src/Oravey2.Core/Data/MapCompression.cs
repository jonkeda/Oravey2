using System.IO.Compression;

namespace Oravey2.Core.Data;

public static class MapCompression
{
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
        {
            brotli.Write(data);
        }
        return output.ToArray();
    }

    public static byte[] Decompress(byte[] compressed, int expectedLength)
    {
        var result = new byte[expectedLength];
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);

        int totalRead = 0;
        while (totalRead < expectedLength)
        {
            int read = brotli.Read(result, totalRead, expectedLength - totalRead);
            if (read == 0)
                throw new InvalidDataException(
                    $"Brotli stream ended after {totalRead} bytes, expected {expectedLength}.");
            totalRead += read;
        }

        return result;
    }
}

using Oravey2.Core.Data;

namespace Oravey2.Tests.Data;

public class MapCompressionTests
{
    [Fact]
    public void Compress_ThenDecompress_ReturnsOriginalBytes()
    {
        var rng = new Random(42);
        var original = new byte[4096];
        rng.NextBytes(original);

        var compressed = MapCompression.Compress(original);
        var result = MapCompression.Decompress(compressed, original.Length);

        Assert.Equal(original, result);
    }

    [Fact]
    public void Compress_EmptyInput_ReturnsValidOutput()
    {
        var compressed = MapCompression.Compress(ReadOnlySpan<byte>.Empty);
        var result = MapCompression.Decompress(compressed, 0);

        Assert.Empty(result);
    }
}

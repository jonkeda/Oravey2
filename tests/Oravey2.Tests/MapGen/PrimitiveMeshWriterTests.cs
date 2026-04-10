using System.Buffers.Binary;
using Oravey2.MapGen.Assets;

namespace Oravey2.Tests.MapGen;

public class PrimitiveMeshWriterTests
{
    [Fact]
    public void BuildPyramid_ProducesValidGlb()
    {
        var glb = PrimitiveMeshWriter.BuildPyramid();
        AssertValidGlbHeader(glb);
    }

    [Fact]
    public void BuildCube_ProducesValidGlb()
    {
        var glb = PrimitiveMeshWriter.BuildCube();
        AssertValidGlbHeader(glb);
    }

    [Fact]
    public void BuildSphere_ProducesValidGlb()
    {
        var glb = PrimitiveMeshWriter.BuildSphere();
        AssertValidGlbHeader(glb);
    }

    [Fact]
    public void BuildPyramid_HasNonTrivialSize()
    {
        var glb = PrimitiveMeshWriter.BuildPyramid();
        Assert.True(glb.Length > 100, "Pyramid GLB should be more than 100 bytes");
    }

    [Fact]
    public void BuildCube_HasNonTrivialSize()
    {
        var glb = PrimitiveMeshWriter.BuildCube();
        Assert.True(glb.Length > 100, "Cube GLB should be more than 100 bytes");
    }

    [Fact]
    public void BuildSphere_HasNonTrivialSize()
    {
        var glb = PrimitiveMeshWriter.BuildSphere();
        Assert.True(glb.Length > 100, "Sphere GLB should be more than 100 bytes");
    }

    [Fact]
    public void EnsurePrimitiveMeshes_CreatesThreeFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        try
        {
            PrimitiveMeshWriter.EnsurePrimitiveMeshes(tempDir);

            var meshDir = Path.Combine(tempDir, "assets", "meshes", "primitives");
            Assert.True(File.Exists(Path.Combine(meshDir, "pyramid.glb")));
            Assert.True(File.Exists(Path.Combine(meshDir, "cube.glb")));
            Assert.True(File.Exists(Path.Combine(meshDir, "sphere.glb")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void EnsurePrimitiveMeshes_DoesNotOverwriteExisting()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        try
        {
            PrimitiveMeshWriter.EnsurePrimitiveMeshes(tempDir);
            var pyramidPath = Path.Combine(tempDir, "assets", "meshes", "primitives", "pyramid.glb");
            var originalBytes = File.ReadAllBytes(pyramidPath);

            // Write a marker file in its place
            File.WriteAllBytes(pyramidPath, [0xDE, 0xAD]);

            PrimitiveMeshWriter.EnsurePrimitiveMeshes(tempDir);

            var afterBytes = File.ReadAllBytes(pyramidPath);
            Assert.Equal(2, afterBytes.Length); // was not overwritten
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(16, 8)]
    [InlineData(8, 4)]
    public void BuildSphere_ParameterisedSlicesAndStacks(int slices, int stacks)
    {
        var glb = PrimitiveMeshWriter.BuildSphere(slices, stacks);
        AssertValidGlbHeader(glb);
    }

    private static void AssertValidGlbHeader(byte[] glb)
    {
        Assert.True(glb.Length >= 12, "GLB must be at least 12 bytes (header)");

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(0, 4));
        Assert.Equal(0x46546C67u, magic); // "glTF"

        var version = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4, 4));
        Assert.Equal(2u, version);

        var totalLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8, 4));
        Assert.Equal((uint)glb.Length, totalLength);
    }
}

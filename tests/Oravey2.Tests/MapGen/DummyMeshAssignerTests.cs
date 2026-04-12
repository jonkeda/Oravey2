using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.MapGen;

public class DummyMeshAssignerTests
{
    private static TownDesign CreateTestDesign() => new(
        "TestTown",
        [new LandmarkBuilding("The Beacon", "A towering lighthouse", "large", "", "", "")],
        [
            new KeyLocation("Harbor Dock", "shop", "A dock complex", "medium", "", "", ""),
            new KeyLocation("Watchtower", "military", "A tall watchtower", "small", "", "", ""),
        ],
        "organic",
        []);

    private static PlacedBuilding MakeBuilding(string name, string mesh) => new(
        $"building_{name.GetHashCode():X}",
        name,
        mesh,
        "medium",
        [[0, 0]],
        2,
        0.5f,
        new TilePlacement(0, 0, 0, 0));

    private static PlacedProp MakeProp(string mesh) => new(
        "prop_0",
        mesh,
        new TilePlacement(0, 0, 1, 1),
        45f,
        1f,
        false);

    [Fact]
    public void ClassifyBuilding_Landmark_ReturnsLandmark()
    {
        var design = CreateTestDesign();
        Assert.Equal("landmark", DummyMeshAssigner.ClassifyBuilding("The Beacon", design));
    }

    [Fact]
    public void ClassifyBuilding_KeyLocation_ReturnsKey()
    {
        var design = CreateTestDesign();
        Assert.Equal("key", DummyMeshAssigner.ClassifyBuilding("Harbor Dock", design));
        Assert.Equal("key", DummyMeshAssigner.ClassifyBuilding("Watchtower", design));
    }

    [Fact]
    public void ClassifyBuilding_Generic_ReturnsGeneric()
    {
        var design = CreateTestDesign();
        Assert.Equal("generic", DummyMeshAssigner.ClassifyBuilding("Ruin 5", design));
    }

    [Fact]
    public void ClassifyBuilding_IsCaseInsensitive()
    {
        var design = CreateTestDesign();
        Assert.Equal("landmark", DummyMeshAssigner.ClassifyBuilding("the beacon", design));
        Assert.Equal("key", DummyMeshAssigner.ClassifyBuilding("HARBOR DOCK", design));
    }

    [Fact]
    public void PrimitiveMeshFor_Landmark_ReturnsPyramid()
    {
        Assert.Equal(PrimitiveMeshWriter.PyramidPath, DummyMeshAssigner.PrimitiveMeshFor("landmark"));
    }

    [Fact]
    public void PrimitiveMeshFor_Key_ReturnsCube()
    {
        Assert.Equal(PrimitiveMeshWriter.CubePath, DummyMeshAssigner.PrimitiveMeshFor("key"));
    }

    [Fact]
    public void PrimitiveMeshFor_Generic_ReturnsCube()
    {
        Assert.Equal(PrimitiveMeshWriter.CubePath, DummyMeshAssigner.PrimitiveMeshFor("generic"));
    }

    [Fact]
    public void AssignPrimitiveMeshes_LandmarkGetsPyramid()
    {
        var design = CreateTestDesign();
        var buildings = new List<PlacedBuilding>
        {
            MakeBuilding("The Beacon", "meshes/the_beacon.glb"),
        };

        var assigner = new DummyMeshAssigner();
        var (updated, _) = assigner.AssignPrimitiveMeshes(design, buildings, []);

        Assert.Single(updated);
        Assert.Equal(PrimitiveMeshWriter.PyramidPath, updated[0].MeshAsset);
    }

    [Fact]
    public void AssignPrimitiveMeshes_KeyLocationGetsCube()
    {
        var design = CreateTestDesign();
        var buildings = new List<PlacedBuilding>
        {
            MakeBuilding("Harbor Dock", "meshes/harbor_dock.glb"),
        };

        var assigner = new DummyMeshAssigner();
        var (updated, _) = assigner.AssignPrimitiveMeshes(design, buildings, []);

        Assert.Single(updated);
        Assert.Equal(PrimitiveMeshWriter.CubePath, updated[0].MeshAsset);
    }

    [Fact]
    public void AssignPrimitiveMeshes_GenericBuildingGetsCube()
    {
        var design = CreateTestDesign();
        var buildings = new List<PlacedBuilding>
        {
            MakeBuilding("Ruin 3", "meshes/generic_ruin.glb"),
        };

        var assigner = new DummyMeshAssigner();
        var (updated, _) = assigner.AssignPrimitiveMeshes(design, buildings, []);

        Assert.Single(updated);
        Assert.Equal(PrimitiveMeshWriter.CubePath, updated[0].MeshAsset);
    }

    [Fact]
    public void AssignPrimitiveMeshes_PropsGetSphere()
    {
        var design = CreateTestDesign();
        var props = new List<PlacedProp>
        {
            MakeProp("meshes/barrel.glb"),
            MakeProp("meshes/crate.glb"),
        };

        var assigner = new DummyMeshAssigner();
        var (_, updated) = assigner.AssignPrimitiveMeshes(design, [], props);

        Assert.Equal(2, updated.Count);
        Assert.All(updated, p => Assert.Equal(PrimitiveMeshWriter.SpherePath, p.MeshAsset));
    }

    [Fact]
    public void AssignPrimitiveMeshes_PreservesAcceptedMeshes()
    {
        var design = CreateTestDesign();
        var realMesh = "meshes/meshy_the_beacon.glb";
        var buildings = new List<PlacedBuilding>
        {
            MakeBuilding("The Beacon", realMesh),
        };
        var props = new List<PlacedProp>
        {
            MakeProp("meshes/meshy_barrel.glb"),
        };

        var assigner = new DummyMeshAssigner([realMesh, "meshes/meshy_barrel.glb"]);
        var (updatedB, updatedP) = assigner.AssignPrimitiveMeshes(design, buildings, props);

        Assert.Equal(realMesh, updatedB[0].MeshAsset);
        Assert.Equal("meshes/meshy_barrel.glb", updatedP[0].MeshAsset);
    }

    [Fact]
    public void AssignPrimitiveMeshes_MixedAcceptedAndPlaceholder()
    {
        var design = CreateTestDesign();
        var realMesh = "meshes/meshy_harbor_dock.glb";
        var buildings = new List<PlacedBuilding>
        {
            MakeBuilding("The Beacon", "meshes/the_beacon.glb"),      // placeholder → pyramid
            MakeBuilding("Harbor Dock", realMesh),                     // accepted → keep
            MakeBuilding("Ruin 1", "meshes/generic_ruin.glb"),        // placeholder → cube
        };

        var assigner = new DummyMeshAssigner([realMesh]);
        var (updated, _) = assigner.AssignPrimitiveMeshes(design, buildings, []);

        Assert.Equal(PrimitiveMeshWriter.PyramidPath, updated[0].MeshAsset);
        Assert.Equal(realMesh, updated[1].MeshAsset);
        Assert.Equal(PrimitiveMeshWriter.CubePath, updated[2].MeshAsset);
    }

    [Fact]
    public void AssignPrimitiveMeshes_EmptyInputs_ReturnsEmpty()
    {
        var design = CreateTestDesign();
        var assigner = new DummyMeshAssigner();
        var (buildings, props) = assigner.AssignPrimitiveMeshes(design, [], []);

        Assert.Empty(buildings);
        Assert.Empty(props);
    }

    [Fact]
    public void AssignPrimitiveMeshes_PreservesNonMeshFields()
    {
        var design = CreateTestDesign();
        var original = new PlacedBuilding(
            "building_0", "The Beacon", "meshes/the_beacon.glb", "large",
            [[5, 5], [5, 6], [6, 5], [6, 6]], 3, 0.6f,
            new TilePlacement(0, 0, 5, 5));

        var assigner = new DummyMeshAssigner();
        var (updated, _) = assigner.AssignPrimitiveMeshes(design, [original], []);

        var b = updated[0];
        Assert.Equal("building_0", b.Id);
        Assert.Equal("The Beacon", b.Name);
        Assert.Equal("large", b.SizeCategory);
        Assert.Equal(3, b.Floors);
        Assert.Equal(0.6f, b.Condition);
        Assert.Equal(PrimitiveMeshWriter.PyramidPath, b.MeshAsset);
    }
}

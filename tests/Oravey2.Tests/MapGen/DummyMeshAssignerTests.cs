using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.MapGen;

public class DummyMeshAssignerTests
{
    private static TownDesign CreateTestDesign() => new()
    {
        TownName = "TestTown",
        Landmarks = [new LandmarkBuilding { Name = "The Beacon", VisualDescription = "A towering lighthouse", SizeCategory = "large", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" }],
        KeyLocations =
        [
            new KeyLocation { Name = "Harbor Dock", Purpose = "shop", VisualDescription = "A dock complex", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            new KeyLocation { Name = "Watchtower", Purpose = "military", VisualDescription = "A tall watchtower", SizeCategory = "small", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
        ],
        LayoutStyle = "organic",
        Hazards = [],
    };

    private static BuildingDto MakeBuilding(string name, string mesh) => new()
    {
        Id = $"building_{name.GetHashCode():X}",
        Name = name,
        MeshAsset = mesh,
        Size = "medium",
        Footprint = [[0, 0]],
        Floors = 2,
        Condition = 0.5f,
        Placement = new PlacementDto(0, 0, 0, 0),
    };

    private static PropDto MakeProp(string mesh) => new()
    {
        Id = "prop_0",
        MeshAsset = mesh,
        Placement = new PlacementDto(0, 0, 1, 1),
        Rotation = 45f,
        Scale = 1f,
        BlocksWalkability = false,
    };

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
        var buildings = new List<BuildingDto>
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
        var buildings = new List<BuildingDto>
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
        var buildings = new List<BuildingDto>
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
        var props = new List<PropDto>
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
        var buildings = new List<BuildingDto>
        {
            MakeBuilding("The Beacon", realMesh),
        };
        var props = new List<PropDto>
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
        var buildings = new List<BuildingDto>
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
        var original = new BuildingDto
        {
            Id = "building_0", Name = "The Beacon", MeshAsset = "meshes/the_beacon.glb", Size = "large",
            Footprint = [[5, 5], [5, 6], [6, 5], [6, 6]], Floors = 3, Condition = 0.6f,
            Placement = new PlacementDto(0, 0, 5, 5),
        };

        var assigner = new DummyMeshAssigner();
        var (updated, _) = assigner.AssignPrimitiveMeshes(design, [original], []);

        var b = updated[0];
        Assert.Equal("building_0", b.Id);
        Assert.Equal("The Beacon", b.Name);
        Assert.Equal("large", b.Size);
        Assert.Equal(3, b.Floors);
        Assert.Equal(0.6f, b.Condition);
        Assert.Equal(PrimitiveMeshWriter.PyramidPath, b.MeshAsset);
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Oravey2.Contracts.Spatial;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class BuildSpatialSpecificationTests
{
    [Fact]
    public void Build_ValidInput_ReturnsSpatialSpec()
    {
        var spec = BuildSpatialSpecification.Build(CreateTestLlmSpec(), CreateTestDesign());
        
        Assert.NotNull(spec);
        Assert.Equal(2, spec.BuildingPlacements.Count);
        Assert.Single(spec.RoadNetwork.Edges);
    }

    [Fact]
    public void Build_ConvertsBuildingNames()
    {
        var llmSpec = CreateTestLlmSpec();
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        Assert.Contains("Cathedral", spec.BuildingPlacements.Keys);
        Assert.Contains("Market", spec.BuildingPlacements.Keys);
    }

    [Fact]
    public void Build_ConvertsBoundingBox()
    {
        var llmSpec = CreateTestLlmSpec();
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        Assert.Equal(52.0, spec.RealWorldBounds.MinLat);
        Assert.Equal(53.0, spec.RealWorldBounds.MaxLat);
        Assert.Equal(4.0, spec.RealWorldBounds.MinLon);
        Assert.Equal(5.0, spec.RealWorldBounds.MaxLon);
    }

    [Fact]
    public void Build_ConvertsBuildingFootprints()
    {
        var llmSpec = CreateTestLlmSpec();
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        var cathedral = spec.BuildingPlacements["Cathedral"];
        
        Assert.Equal(40.0, cathedral.WidthMeters, 0.1);
        Assert.Equal(50.0, cathedral.DepthMeters, 0.1);
        Assert.Equal(45.0, cathedral.RotationDegrees, 0.1);
    }

    [Fact]
    public void Build_ConvertsRoadNetwork()
    {
        var llmSpec = CreateTestLlmSpec();
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        Assert.Single(spec.RoadNetwork.Edges);
        var edge = spec.RoadNetwork.Edges[0];
        Assert.Equal(52.5, edge.FromLat, 0.01);
        Assert.Equal(4.5, edge.FromLon, 0.01);
    }

    [Fact]
    public void Build_ConvertsWaterBodies()
    {
        var llmSpec = CreateTestLlmSpecWithWater();
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        Assert.Single(spec.WaterBodies);
        Assert.Equal("Main Canal", spec.WaterBodies[0].Name);
        Assert.Equal(SpatialWaterType.Canal, spec.WaterBodies[0].Type);
    }

    [Fact]
    public void Build_InvalidBuildingName_FiltersOut()
    {
        var llmSpec = CreateTestLlmSpec();
        llmSpec.BuildingPlacements[0].BuildingName = "Unknown Building";
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        // Invalid name filtered out, only "Market" remains
        Assert.Single(spec.BuildingPlacements);
        Assert.Contains("Market", spec.BuildingPlacements.Keys);
    }

    [Fact]
    public void Build_SwappedBoundingBox_AutoRepairs()
    {
        var llmSpec = CreateTestLlmSpec();
        llmSpec.RealWorldBounds.MinLat = 53.0;  // Swapped
        llmSpec.RealWorldBounds.MaxLat = 52.0;
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        Assert.True(spec.RealWorldBounds.MinLat < spec.RealWorldBounds.MaxLat);
        Assert.Equal(52.0, spec.RealWorldBounds.MinLat);
        Assert.Equal(53.0, spec.RealWorldBounds.MaxLat);
    }

    [Fact]
    public void Build_ZeroBuildingFootprint_DefaultsTo10m()
    {
        var llmSpec = CreateTestLlmSpec();
        llmSpec.BuildingPlacements[0].WidthMeters = 0;  // Invalid → defaults to 10
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        var cathedral = spec.BuildingPlacements["Cathedral"];
        
        Assert.Equal(10.0, cathedral.WidthMeters, 0.1);
    }

    [Fact]
    public void Build_BuildingOutsideBounds_Allows()
    {
        // In Phase 1, buildings outside nominal bounds are allowed since transforms will clamp them
        var llmSpec = CreateTestLlmSpec();
        llmSpec.BuildingPlacements[0].CenterLat = 54.0;  // Outside bounds
        var design = CreateTestDesign();
        
        // Should not throw — building will be clamped during tile transform
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        Assert.NotNull(spec);
    }

    [Fact]
    public void Build_NoRoadEdges_SynthesizesCenterRoad()
    {
        var llmSpec = CreateTestLlmSpec();
        llmSpec.RoadNetwork.Edges.Clear();
        var design = CreateTestDesign();
        
        var spec = BuildSpatialSpecification.Build(llmSpec, design);
        
        // A synthetic road through the bounding box centre is created
        Assert.Single(spec.RoadNetwork.Edges);
        var edge = spec.RoadNetwork.Edges[0];
        double midLat = (52.0 + 53.0) / 2;
        Assert.Equal(midLat, edge.FromLat, 0.01);
        Assert.Equal(midLat, edge.ToLat, 0.01);
    }

    private LlmTownSpatialSpec CreateTestLlmSpec()
    {
        var bbox = new LlmBoundingBoxDto { MinLat = 52.0, MaxLat = 53.0, MinLon = 4.0, MaxLon = 5.0 };
        
        var buildings = new List<LlmBuildingPlacementDto>
        {
            new() 
            {
                BuildingName = "Cathedral",
                CenterLat = 52.5,
                CenterLon = 4.5,
                WidthMeters = 40.0,
                DepthMeters = 50.0,
                RotationDegrees = 45.0,
                AlignmentHint = "square_corner"
            },
            new()
            {
                BuildingName = "Market",
                CenterLat = 52.4,
                CenterLon = 4.4,
                WidthMeters = 20.0,
                DepthMeters = 30.0,
                RotationDegrees = 0.0,
                AlignmentHint = "on_main_road"
            }
        };

        var roadNodes = new List<LlmCoordinateDto>
        {
            new() { Lat = 52.5, Lon = 4.5 },
            new() { Lat = 52.4, Lon = 4.4 }
        };

        var roadEdges = new List<LlmRoadEdgeDto>
        {
            new() { FromNodeIndex = 0, ToNodeIndex = 1, RoadType = "main" }
        };

        var roadNetwork = new LlmRoadNetworkDto
        {
            Nodes = roadNodes,
            Edges = roadEdges,
            RoadWidthMeters = 10.0f
        };

        return new LlmTownSpatialSpec
        {
            RealWorldBounds = bbox,
            BuildingPlacements = buildings,
            RoadNetwork = roadNetwork,
            WaterBodies = [],
            TerrainDescription = "flat"
        };
    }

    private LlmTownSpatialSpec CreateTestLlmSpecWithWater()
    {
        var baseSpec = CreateTestLlmSpec();
        
        var waterPolygon = new List<LlmCoordinateDto>
        {
            new() { Lat = 52.3, Lon = 4.3 },
            new() { Lat = 52.7, Lon = 4.3 },
            new() { Lat = 52.7, Lon = 4.7 },
            new() { Lat = 52.3, Lon = 4.7 }
        };

        baseSpec.WaterBodies = new List<LlmWaterBodyDto>
        {
            new()
            {
                Name = "Main Canal",
                Type = "canal",
                Polygon = waterPolygon
            }
        };

        return baseSpec;
    }

    [Fact]
    public void Build_DuplicateBuildingNames_TakesFirstWithoutThrowing()
    {
        var llmSpec = CreateTestLlmSpec();

        // Add a duplicate "Cathedral" with different coordinates
        llmSpec.BuildingPlacements.Add(new LlmBuildingPlacementDto
        {
            BuildingName = "Cathedral",
            CenterLat = 52.6,
            CenterLon = 4.6,
            WidthMeters = 30.0,
            DepthMeters = 35.0,
            RotationDegrees = 90.0,
            AlignmentHint = "on_main_road"
        });

        var design = CreateTestDesign();

        // Should not throw — duplicates are silently de-duped (first wins)
        var spec = BuildSpatialSpecification.Build(llmSpec, design);

        Assert.NotNull(spec);
        Assert.Equal(2, spec.BuildingPlacements.Count); // Cathedral + Market
        Assert.Contains("Cathedral", spec.BuildingPlacements.Keys);

        // Verify first placement was kept (original at 52.5, not duplicate at 52.6)
        var cathedral = spec.BuildingPlacements["Cathedral"];
        Assert.Equal(52.5, cathedral.CenterLat, 0.01);
    }

    private TownDesign CreateTestDesign()
    {
        var landmarks = new List<LandmarkBuilding>
        {
            new LandmarkBuilding { Name = "Cathedral", VisualDescription = "A ruined cathedral", SizeCategory = "large", OriginalDescription = "Original Cathedral", MeshyPrompt = "Gothic ruin", PositionHint = "centre" }
        };

        var locations = new List<KeyLocation>
        {
            new KeyLocation { Name = "Market", Purpose = "shop", VisualDescription = "Marketplace ruins", SizeCategory = "medium", OriginalDescription = "Former market square", MeshyPrompt = "Trading hub", PositionHint = "centre, on the main square" }
        };

        return new TownDesign { TownName = "Purgatory", Landmarks = landmarks, KeyLocations = locations, LayoutStyle = "grid", Hazards = [] };
    }
}

using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.MapGen.Generation;

public sealed class RegionGenerator
{
    public RegionData Generate(
        RegionTemplate region,
        ContinentData continent,
        CuratedRegion curated,
        int seed)
    {
        var rng = new Random(seed ^ region.Name.GetHashCode());

        var elevGrid = region.ElevationGrid;
        int rows = elevGrid.GetLength(0);
        int cols = elevGrid.GetLength(1);
        double cellSize = region.GridCellSizeMetres;

        // Build heightmap with detail noise
        var heightmap = new float[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;
                heightmap[r, c] = elevGrid[r, c] + noise;
            }
        }

        // Build biome grid
        var biomeGrid = new string[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float worldX = (float)(c * cellSize);
                float worldZ = (float)(r * cellSize);
                biomeGrid[r, c] = ClassifyBiome(region, new Vector2(worldX, worldZ), elevGrid[r, c]);
            }
        }

        // Town POIs from curated plan
        var pois = curated.Towns.Select(t => new PoiMarker(
            t.GameName, "town", t.GamePosition, t.Description)).ToList();

        // Linear features (roads, rivers) from WorldTemplate
        var linearFeatures = new List<LinearFeatureData>();
        foreach (var road in region.Roads)
        {
            var featureType = road.RoadClass switch
            {
                RoadClass.Motorway => LinearFeatureType.Highway,
                RoadClass.Trunk => LinearFeatureType.Road,
                RoadClass.Primary => LinearFeatureType.Road,
                RoadClass.Secondary => LinearFeatureType.DirtRoad,
                _ => LinearFeatureType.Path
            };
            float width = road.RoadClass switch
            {
                RoadClass.Motorway => 12f,
                RoadClass.Trunk => 8f,
                RoadClass.Primary => 6f,
                _ => 4f
            };
            linearFeatures.Add(new LinearFeatureData(featureType, width, road.Nodes));
        }

        foreach (var water in region.WaterBodies)
        {
            var featureType = water.Type switch
            {
                WaterType.River => LinearFeatureType.River,
                WaterType.Canal => LinearFeatureType.Canal,
                _ => LinearFeatureType.Stream
            };
            float width = water.Type switch
            {
                WaterType.River => 20f,
                WaterType.Canal => 8f,
                _ => 4f
            };
            if (water.Type == WaterType.River || water.Type == WaterType.Canal || water.Type == WaterType.Sea)
                linearFeatures.Add(new LinearFeatureData(featureType, width, water.Geometry));
        }

        foreach (var rail in region.Railways)
        {
            linearFeatures.Add(new LinearFeatureData(LinearFeatureType.Rail, 4f, rail.Nodes));
        }

        return new RegionData(
            region.Name,
            heightmap,
            biomeGrid,
            cellSize,
            pois,
            linearFeatures);
    }

    private static string ClassifyBiome(RegionTemplate region, Vector2 point, float elevation)
    {
        foreach (var zone in region.LandUseZones)
        {
            if (ContinentGenerator.PointInPolygon(point, zone.Polygon))
            {
                return zone.Type switch
                {
                    LandUseType.Forest => "forest",
                    LandUseType.Farmland => "farmland",
                    LandUseType.Residential or LandUseType.Commercial or LandUseType.Industrial => "urban",
                    LandUseType.Meadow or LandUseType.Orchard => "grassland",
                    _ => "wasteland"
                };
            }
        }
        if (elevation < 0) return "water";
        if (elevation > 500) return "highland";
        return "wasteland";
    }
}

public sealed record RegionData(
    string Name,
    float[,] Heightmap,
    string[,] BiomeGrid,
    double CellSizeMetres,
    List<PoiMarker> Pois,
    List<LinearFeatureData> LinearFeatures);

public sealed record PoiMarker(string Name, string Type, Vector2 Position, string? Description = null);

public sealed record LinearFeatureData(LinearFeatureType Type, float Width, Vector2[] Nodes);

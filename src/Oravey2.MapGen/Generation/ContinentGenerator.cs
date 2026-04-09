using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class ContinentGenerator
{
    private const int SampleResolutionMetres = 1000;

    public ContinentData Generate(RegionTemplateFile template)
    {
        int gridWidth = 1;
        int gridHeight = 1;
        var biomes = new string[1, 1];
        var elevations = new double[1, 1];

        if (template.Regions.Count > 0)
        {
            var region = template.Regions[0];
            var elevGrid = region.ElevationGrid;
            int srcRows = elevGrid.GetLength(0);
            int srcCols = elevGrid.GetLength(1);

            double cellSize = region.GridCellSizeMetres;
            int step = Math.Max(1, (int)(SampleResolutionMetres / cellSize));

            gridWidth = Math.Max(1, srcCols / step);
            gridHeight = Math.Max(1, srcRows / step);
            elevations = new double[gridHeight, gridWidth];
            biomes = new string[gridHeight, gridWidth];

            for (int gy = 0; gy < gridHeight; gy++)
            {
                for (int gx = 0; gx < gridWidth; gx++)
                {
                    int srcR = Math.Min(gy * step, srcRows - 1);
                    int srcC = Math.Min(gx * step, srcCols - 1);
                    elevations[gy, gx] = elevGrid[srcR, srcC];
                    biomes[gy, gx] = ClassifyBiome(region, srcR, srcC, cellSize, elevGrid[srcR, srcC]);
                }
            }
        }

        return new ContinentData(template.Name, gridWidth, gridHeight, elevations, biomes);
    }

    private static string ClassifyBiome(RegionTemplate region, int row, int col, double cellSize, float elevation)
    {
        float worldX = (float)(col * cellSize);
        float worldZ = (float)(row * cellSize);
        var point = new Vector2(worldX, worldZ);

        foreach (var zone in region.LandUseZones)
        {
            if (PointInPolygon(point, zone.Polygon))
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

    internal static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}

public sealed record ContinentData(
    string Name,
    int GridWidth,
    int GridHeight,
    double[,] Elevations,
    string[,] Biomes);

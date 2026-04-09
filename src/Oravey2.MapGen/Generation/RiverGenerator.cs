using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class RiverGenerator
{
    public List<LinearFeatureData> Generate(RegionTemplate region)
    {
        var result = new List<LinearFeatureData>();

        foreach (var water in region.WaterBodies)
        {
            var (type, width) = water.Type switch
            {
                WaterType.River => (LinearFeatureType.River, 20f),
                WaterType.Canal => (LinearFeatureType.Canal, 8f),
                WaterType.Sea => (LinearFeatureType.Stream, 4f),
                WaterType.Lake => (LinearFeatureType.Stream, 4f),
                _ => (LinearFeatureType.Stream, 4f)
            };

            // Rivers and canals are linear features; lakes are skipped as linear
            if (water.Type == WaterType.River || water.Type == WaterType.Canal)
            {
                result.Add(new LinearFeatureData(type, width, water.Geometry));
            }
        }

        return result;
    }
}

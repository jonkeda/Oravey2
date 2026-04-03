using System.Text.Json;
using Oravey2.MapGen.Spatial;

namespace Oravey2.MapGen.Tools;

public sealed class CheckOverlapTool
{
    public string Handle(BuildingFootprint[] footprints)
    {
        var overlaps = SpatialUtils.FindOverlaps(footprints);

        return JsonSerializer.Serialize(new
        {
            hasOverlaps = overlaps.Count > 0,
            overlaps = overlaps.Select(o => new { a = o.A, b = o.B }).ToArray()
        });
    }
}

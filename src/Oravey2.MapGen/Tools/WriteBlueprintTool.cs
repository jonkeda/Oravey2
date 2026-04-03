using System.Text.Json;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Spatial;
using Oravey2.MapGen.Validation;

namespace Oravey2.MapGen.Tools;

public sealed class WriteBlueprintTool
{
    private readonly IBlueprintValidator _validator;

    public MapBlueprint? LastAcceptedBlueprint { get; private set; }
    public string? LastAcceptedJson { get; private set; }

    public WriteBlueprintTool(IBlueprintValidator validator)
    {
        _validator = validator;
    }

    public string Handle(string blueprintJson)
    {
        MapBlueprint? blueprint;
        try
        {
            blueprint = BlueprintLoader.LoadFromString(blueprintJson);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                accepted = false,
                errors = new[] { $"JSON parse error: {ex.Message}" }
            });
        }

        var errors = new List<string>();

        // Validate against terrain rules
        var validation = _validator.Validate(blueprint);
        if (!validation.IsValid)
        {
            errors.AddRange(validation.Errors.Select(e => $"[{e.Code}] {e.Message}"));
        }

        // Check building overlaps
        if (blueprint.Buildings is { Length: > 0 })
        {
            var footprints = blueprint.Buildings.Select(b =>
                new BuildingFootprint(b.Id, b.TileX, b.TileY, b.FootprintWidth, b.FootprintHeight))
                .ToArray();

            var overlaps = SpatialUtils.FindOverlaps(footprints);
            if (overlaps.Count > 0)
            {
                errors.AddRange(overlaps.Select(o => $"Building overlap: {o.A} and {o.B}"));
            }
        }

        if (errors.Count > 0)
        {
            return JsonSerializer.Serialize(new
            {
                accepted = false,
                errors = errors.ToArray()
            });
        }

        LastAcceptedBlueprint = blueprint;
        LastAcceptedJson = blueprintJson;

        return JsonSerializer.Serialize(new
        {
            accepted = true,
            errors = Array.Empty<string>()
        });
    }
}

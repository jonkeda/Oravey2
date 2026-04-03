using System.Text.Json;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Validation;

namespace Oravey2.MapGen.Tools;

public sealed class ValidateBlueprintTool
{
    private readonly IBlueprintValidator _validator;

    public ValidateBlueprintTool(IBlueprintValidator validator)
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
            return JsonSerializer.Serialize(new { valid = false, errors = new[] { $"JSON parse error: {ex.Message}" } });
        }

        var result = _validator.Validate(blueprint);

        return JsonSerializer.Serialize(new
        {
            valid = result.IsValid,
            errors = result.Errors.Select(e => $"[{e.Code}] {e.Message}").ToArray()
        });
    }
}

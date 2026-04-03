using Oravey2.Core.World.Blueprint;

namespace Oravey2.MapGen.Validation;

public interface IBlueprintValidator
{
    ValidationResult Validate(MapBlueprint blueprint);
}

using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Generation;

namespace Oravey2.MapGen.Assets;

/// <summary>
/// Assigns primitive placeholder meshes to buildings and props based on their
/// classification: pyramid for landmarks, cube for unique/key buildings,
/// sphere for props. Skips entries that already have a non-primitive mesh
/// (e.g., a Meshy-generated asset).
/// </summary>
public sealed class DummyMeshAssigner
{
    private readonly HashSet<string> _acceptedMeshPaths;

    /// <param name="acceptedMeshPaths">
    /// Set of mesh paths that have been accepted from Meshy (or manually provided).
    /// Buildings/props whose <c>MeshAsset</c> matches one of these paths will not
    /// be overwritten with a primitive.
    /// </param>
    public DummyMeshAssigner(IEnumerable<string>? acceptedMeshPaths = null)
    {
        _acceptedMeshPaths = acceptedMeshPaths is not null
            ? new HashSet<string>(acceptedMeshPaths, StringComparer.OrdinalIgnoreCase)
            : [];
    }

    /// <summary>
    /// Returns new building and prop lists with primitive mesh paths assigned
    /// where the current mesh is a placeholder.
    /// </summary>
    public (List<BuildingDto> Buildings, List<PropDto> Props) AssignPrimitiveMeshes(
        TownDesign design,
        List<BuildingDto> buildings,
        List<PropDto> props)
    {
        var updatedBuildings = new List<BuildingDto>(buildings.Count);
        foreach (var b in buildings)
        {
            if (IsAcceptedMesh(b.MeshAsset))
            {
                updatedBuildings.Add(b);
                continue;
            }

            var classification = ClassifyBuilding(b.Name, design);
            var meshPath = PrimitiveMeshFor(classification);
            var updated = new BuildingDto
            {
                Id = b.Id, Name = b.Name, MeshAsset = meshPath, Size = b.Size,
                Footprint = b.Footprint, Floors = b.Floors, Condition = b.Condition, Placement = b.Placement,
            };
            updatedBuildings.Add(updated);
        }

        var updatedProps = new List<PropDto>(props.Count);
        foreach (var p in props)
        {
            if (IsAcceptedMesh(p.MeshAsset))
            {
                updatedProps.Add(p);
                continue;
            }

            var updatedProp = new PropDto
            {
                Id = p.Id, MeshAsset = PrimitiveMeshWriter.SpherePath,
                Placement = p.Placement, Rotation = p.Rotation, Scale = p.Scale, BlocksWalkability = p.BlocksWalkability,
            };
            updatedProps.Add(updatedProp);
        }

        return (updatedBuildings, updatedProps);
    }

    /// <summary>
    /// Classifies a building by matching its name against the town design.
    /// Returns "landmark", "key", or "generic".
    /// </summary>
    public static string ClassifyBuilding(string buildingName, TownDesign design)
    {
        if (design.Landmarks.Any(lm =>
                string.Equals(lm.Name, buildingName, StringComparison.OrdinalIgnoreCase)))
            return "landmark";

        if (design.KeyLocations.Any(kl =>
                string.Equals(kl.Name, buildingName, StringComparison.OrdinalIgnoreCase)))
            return "key";

        return "generic";
    }

    /// <summary>
    /// Returns the primitive mesh path for a building classification.
    /// </summary>
    public static string PrimitiveMeshFor(string classification)
        => classification switch
        {
            "landmark" => PrimitiveMeshWriter.PyramidPath,
            "key" => PrimitiveMeshWriter.CubePath,
            _ => PrimitiveMeshWriter.CubePath,
        };

    /// <summary>
    /// Returns true if the mesh path is in the set of accepted (real) meshes.
    /// </summary>
    private bool IsAcceptedMesh(string meshAsset)
        => !string.IsNullOrEmpty(meshAsset) && _acceptedMeshPaths.Contains(meshAsset);
}

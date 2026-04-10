# Step 09b — Dummy Mesh Assigner

## Goal

Provide immediate visual feedback for placed buildings and props by assigning
simple primitive meshes based on entity type. This replaces the non-existent
placeholder `.glb` references from Step 08 with actual renderable geometry,
allowing the town layout to be visualised before any Meshy API calls.

Primitive assignments can later be upgraded to Meshy-generated assets (Step 09)
without changing the data schema — only the `meshAsset` path changes.

## Primitive Mapping

| Entity Type      | Primitive Shape | Mesh Path                       |
|------------------|-----------------|---------------------------------|
| Landmark         | Pyramid         | `meshes/primitives/pyramid.glb` |
| Key Location     | Cube            | `meshes/primitives/cube.glb`    |
| Generic Building | Cube            | `meshes/primitives/cube.glb`    |
| Prop             | Sphere          | `meshes/primitives/sphere.glb`  |

## Deliverables

### 9b.1 Primitive mesh files

Three `.glb` files created programmatically by `PrimitiveMeshWriter` and
stored in `content/Oravey2.Apocalyptic/assets/meshes/primitives/`:

- `pyramid.glb` — 4-sided pyramid (5 vertices, 6 triangles)
- `cube.glb` — unit cube (24 vertices with face normals, 12 triangles)
- `sphere.glb` — UV sphere (16 slices × 8 stacks)

`PrimitiveMeshWriter` produces minimal valid glTF 2.0 binary (GLB) files with
positions, normals, and indices. No textures or materials.

### 9b.2 Catalog entries

Add primitive entries to `content/Oravey2.Apocalyptic/catalog.json` under a
new `"primitive"` category so the asset registry can resolve them.

### 9b.3 `DummyMeshAssigner`

```csharp
public sealed class DummyMeshAssigner
{
    public void AssignPrimitiveMeshes(
        TownDesign design,
        List<PlacedBuilding> buildings,
        List<PlacedProp> props);

    public static string ClassifyBuilding(
        string buildingName,
        TownDesign design);  // returns "landmark", "key", or "generic"

    public static string PrimitiveMeshFor(string classification);
}
```

- Matches each building to the design to determine if it is the landmark, a
  key location, or a generic fill.
- Returns new `PlacedBuilding` / `PlacedProp` records with updated
  `MeshAsset` paths (records are immutable, so new instances are created).
- Does **not** overwrite buildings whose `MeshAsset` already points to a
  non-placeholder path (i.e., a Meshy-generated asset). This makes it safe
  to re-run after partial Meshy generation.

### 9b.4 Upgrade path

When a Meshy asset is accepted (Step 09), the `meshAsset` field is updated to
the real path (e.g., `meshes/the_beacon.glb`). `DummyMeshAssigner` skips
buildings that already have a non-primitive mesh, so the two steps compose
safely.

### 9b.5 Tests

- `PrimitiveMeshWriter` — verify each `.glb` is valid (correct header,
  non-zero size)
- `DummyMeshAssigner` — landmark gets pyramid, key locations get cube,
  props get sphere
- Classification logic — edge cases (name matching, generic detection)
- Upgrade preservation — buildings with real mesh paths are not overwritten

## Dependencies

- Step 08 (buildings.json + props.json provide the input)
- Existing: `TownDesign`, `PlacedBuilding`, `PlacedProp` records

## Estimated scope

- New files: `PrimitiveMeshWriter.cs`, `DummyMeshAssigner.cs`
- Modified: `catalog.json` (new primitive category)
- New content: 3 × `.glb` files in assets/meshes/primitives/

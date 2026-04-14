using Oravey2.Core.Rendering;
using Oravey2.Core.World.LinearFeatures;
using Oravey2.Core.World.Liquids;
using Oravey2.Core.World.Rendering;
using Oravey2.Core.World.Terrain;
using Oravey2.Core.World.Vegetation;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Oravey2.Core.World;

/// <summary>
/// Replaces TileMapRendererScript. Renders terrain using the heightmap pipeline
/// instead of per-tile quads. Keeps the same public API so ScenarioLoader and
/// automation handlers can reference it.
/// </summary>
public class HeightmapTerrainScript : SyncScript
{
    public TileMapData? MapData { get; set; }
    public WorldMapData? WorldMap { get; set; }
    public float TileSize { get; set; } = HeightmapMeshGenerator.TileWorldSize;
    public QualityPreset Quality { get; set; } = QualityPreset.Medium;
    public BuildingRegistry? Buildings { get; set; }
    public IReadOnlyList<PropDefinition>? Props { get; set; }
    public int CurrentChunkX { get; set; }
    public int CurrentChunkY { get; set; }

    private readonly List<Entity> _terrainEntities = new();
    private readonly List<GeometricPrimitive> _primitives = new();
    private bool _initialized;

    public override void Start()
    {
        if (MapData != null)
            BuildTerrain();
    }

    public override void Update()
    {
        if (MapData != null && !_initialized)
            BuildTerrain();
    }

    private void BuildTerrain()
    {
        ClearTerrain();

        if (MapData == null) return;

        int chunkSize = ChunkData.Size; // 16
        int chunksX = (MapData.Width + chunkSize - 1) / chunkSize;
        int chunksY = (MapData.Height + chunkSize - 1) / chunkSize;
        float chunkWorldSize = chunkSize * TileSize;

        // World-space offset to centre the entire map around the origin
        float halfWorldX = MapData.Width * TileSize / 2f;
        float halfWorldZ = MapData.Height * TileSize / 2f;

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                // Skip chunks that have no data in WorldMap (sparse region grids)
                if (WorldMap != null && WorldMap.GetChunk(cx, cy) == null)
                    continue;

                // Extract the 16×16 sub-grid for this chunk
                var chunkTiles = new TileData[chunkSize, chunkSize];
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    for (int ly = 0; ly < chunkSize; ly++)
                    {
                        int gx = cx * chunkSize + lx;
                        int gy = cy * chunkSize + ly;
                        if (gx < MapData.Width && gy < MapData.Height)
                            chunkTiles[lx, ly] = MapData.TileDataGrid[gx, gy];
                    }
                }

                var chunkData = WorldMap?.GetChunk(cx, cy) ??
                    new ChunkData(cx, cy, CreateTileMapFromGrid(chunkTiles, chunkSize));

                var terrainMesh = ChunkTerrainBuilder.Build(chunkData, neighbors: null, Quality);

                // Convert pipeline vertices to Stride vertex format
                var strideVerts = ConvertVertices(terrainMesh.Vertices);

                // isLeftHanded=true tells Stride to reverse our CW winding to CCW
                // (Stride is right-handed: CCW = front face)
                var meshData = new GeometricMeshData<VertexPositionNormalTexture>(
                    strideVerts, terrainMesh.Indices, true);
                var geomPrimitive = new GeometricPrimitive(GraphicsDevice, meshData);
                _primitives.Add(geomPrimitive);
                var meshDraw = geomPrimitive.ToMeshDraw();

                // Compute bounding box from vertices — required for frustum culling
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var v in strideVerts)
                {
                    if (v.Position.X < min.X) min.X = v.Position.X;
                    if (v.Position.Y < min.Y) min.Y = v.Position.Y;
                    if (v.Position.Z < min.Z) min.Z = v.Position.Z;
                    if (v.Position.X > max.X) max.X = v.Position.X;
                    if (v.Position.Y > max.Y) max.Y = v.Position.Y;
                    if (v.Position.Z > max.Z) max.Z = v.Position.Z;
                }
                var boundingBox = new BoundingBox(min, max);
                var boundingSphere = BoundingSphere.FromBox(boundingBox);

                // Build entity using the exact same pattern as working capsule/cube entities
                var meshEntity = new Entity($"TerrainChunk_{cx}_{cy}");
                var model = new Model();
                model.Meshes.Add(new Mesh
                {
                    Draw = meshDraw,
                    BoundingBox = boundingBox,
                    BoundingSphere = boundingSphere,
                });
                model.Materials.Add(CreateTerrainMaterial(GetDominantColor(chunkTiles, chunkSize)));
                meshEntity.Add(new ModelComponent(model));

                // Position: chunk origin offset to centre all chunks around world origin
                var chunkPos = new Vector3(
                    cx * chunkWorldSize - halfWorldX,
                    0f,
                    cy * chunkWorldSize - halfWorldZ);
                meshEntity.Transform.Position = chunkPos;

                Entity.AddChild(meshEntity);
                _terrainEntities.Add(meshEntity);

                // Render tile overlay for Hybrid chunks
                if (terrainMesh.Overlay != null && terrainMesh.Overlay.FloorVertices.Length > 0)
                {
                    var overlayVerts = ConvertVertices(terrainMesh.Overlay.FloorVertices);
                    var overlayMeshData = new GeometricMeshData<VertexPositionNormalTexture>(
                        overlayVerts, terrainMesh.Overlay.FloorIndices, true);
                    var overlayPrimitive = new GeometricPrimitive(GraphicsDevice, overlayMeshData);
                    _primitives.Add(overlayPrimitive);
                    var overlayDraw = overlayPrimitive.ToMeshDraw();

                    var oMin = new Vector3(float.MaxValue);
                    var oMax = new Vector3(float.MinValue);
                    foreach (var v in overlayVerts)
                    {
                        if (v.Position.X < oMin.X) oMin.X = v.Position.X;
                        if (v.Position.Y < oMin.Y) oMin.Y = v.Position.Y;
                        if (v.Position.Z < oMin.Z) oMin.Z = v.Position.Z;
                        if (v.Position.X > oMax.X) oMax.X = v.Position.X;
                        if (v.Position.Y > oMax.Y) oMax.Y = v.Position.Y;
                        if (v.Position.Z > oMax.Z) oMax.Z = v.Position.Z;
                    }
                    var oBounds = new BoundingBox(oMin, oMax);
                    oBounds.Minimum.Y -= 0.5f;
                    oBounds.Maximum.Y += 0.5f;
                    var oSphere = BoundingSphere.FromBox(oBounds);

                    var overlayEntity = new Entity($"TileOverlay_{cx}_{cy}");
                    var overlayModel = new Model();
                    overlayModel.Meshes.Add(new Mesh
                    {
                        Draw = overlayDraw,
                        BoundingBox = oBounds,
                        BoundingSphere = oSphere,
                    });
                    // Use a lighter concrete-ish colour to distinguish overlay from terrain
                    overlayModel.Materials.Add(CreateTerrainMaterial(new Color4(0.70f, 0.68f, 0.62f, 1f)));
                    var overlayModelComp = new ModelComponent(overlayModel) { IsShadowCaster = false };
                    overlayEntity.Add(overlayModelComp);
                    overlayEntity.Transform.Position = chunkPos;

                    Entity.AddChild(overlayEntity);
                    _terrainEntities.Add(overlayEntity);

                    // Render structure placeholders (cubes) for Hybrid chunks
                    foreach (var structure in terrainMesh.Overlay.Structures)
                    {
                        var structEntity = CreateStructurePlaceholder(structure, chunkPos);
                        Entity.AddChild(structEntity);
                        _terrainEntities.Add(structEntity);
                    }
                }

                // Render linear feature ribbons for this chunk
                foreach (var ribbon in terrainMesh.LinearFeatureMeshes)
                {
                    if (ribbon.Vertices.Length == 0) continue;

                    var ribbonVerts = ConvertVertices(ribbon.Vertices);
                    var ribbonMeshData = new GeometricMeshData<VertexPositionNormalTexture>(
                        ribbonVerts, ribbon.Indices, true);
                    var ribbonPrimitive = new GeometricPrimitive(GraphicsDevice, ribbonMeshData);
                    _primitives.Add(ribbonPrimitive);
                    var ribbonDraw = ribbonPrimitive.ToMeshDraw();

                    var rMin = new Vector3(float.MaxValue);
                    var rMax = new Vector3(float.MinValue);
                    foreach (var v in ribbonVerts)
                    {
                        if (v.Position.X < rMin.X) rMin.X = v.Position.X;
                        if (v.Position.Y < rMin.Y) rMin.Y = v.Position.Y;
                        if (v.Position.Z < rMin.Z) rMin.Z = v.Position.Z;
                        if (v.Position.X > rMax.X) rMax.X = v.Position.X;
                        if (v.Position.Y > rMax.Y) rMax.Y = v.Position.Y;
                        if (v.Position.Z > rMax.Z) rMax.Z = v.Position.Z;
                    }
                    var rBounds = new BoundingBox(rMin, rMax);
                    // Pad Y extent so thin ribbons aren't culled by frustum tests at distance
                    rBounds.Minimum.Y -= 1f;
                    rBounds.Maximum.Y += 1f;
                    var rSphere = BoundingSphere.FromBox(rBounds);

                    var (r, g, b, a) = LinearFeatureStyles.GetColor(ribbon.Type, ribbon.Style);
                    var ribbonEntity = new Entity($"LinearFeature_{cx}_{cy}_{ribbon.Type}");
                    var ribbonModel = new Model();
                    ribbonModel.Meshes.Add(new Mesh
                    {
                        Draw = ribbonDraw,
                        BoundingBox = rBounds,
                        BoundingSphere = rSphere,
                    });
                    ribbonModel.Materials.Add(CreateTerrainMaterial(new Color4(r, g, b, a)));
                    var ribbonModelComp = new ModelComponent(ribbonModel) { IsShadowCaster = false };
                    ribbonEntity.Add(ribbonModelComp);
                    ribbonEntity.Transform.Position = chunkPos;

                    Entity.AddChild(ribbonEntity);
                    _terrainEntities.Add(ribbonEntity);
                }

                // Render liquid surfaces and waterfalls for this chunk
                if (terrainMesh.Liquids != null)
                {
                    foreach (var liquidMesh in terrainMesh.Liquids.SurfaceMeshes)
                        RenderLiquidMesh(liquidMesh, chunkPos, $"Liquid_{cx}_{cy}_{liquidMesh.Type}");

                    foreach (var cascade in terrainMesh.Liquids.WaterfallMeshes)
                        RenderLiquidMesh(cascade, chunkPos, $"Waterfall_{cx}_{cy}_{cascade.Type}");
                }

                // Render trees for forested tiles in this chunk
                var treeSpawns = TreePlacementHelper.GenerateForChunk(chunkData.Tiles);
                if (treeSpawns.Count > 0 && terrainMesh.Heights.GetLength(0) > 0)
                {
                    var treeMeshes = TreeRenderer.BuildMeshes(
                        treeSpawns, terrainMesh.Heights,
                        terrainMesh.VertsPerSide, terrainMesh.ChunkWorldSize);

                    RenderTreeEntity(treeMeshes, chunkPos, cx, cy);

                    // Billboard LOD: built per-frame based on camera distance (Step 11 zoom levels).
                    // Not rendered statically — BuildBillboards() is available for the LOD system.
                }
            }
        }

        _initialized = true;
    }

    private void RenderLiquidMesh(LiquidMesh liquidMesh, Vector3 chunkPos, string entityName)
    {
        if (liquidMesh.Vertices.Length == 0) return;

        var verts = ConvertVertices(liquidMesh.Vertices);
        var meshData = new GeometricMeshData<VertexPositionNormalTexture>(
            verts, liquidMesh.Indices, true);
        var primitive = new GeometricPrimitive(GraphicsDevice, meshData);
        _primitives.Add(primitive);
        var draw = primitive.ToMeshDraw();

        var lMin = new Vector3(float.MaxValue);
        var lMax = new Vector3(float.MinValue);
        foreach (var v in verts)
        {
            if (v.Position.X < lMin.X) lMin.X = v.Position.X;
            if (v.Position.Y < lMin.Y) lMin.Y = v.Position.Y;
            if (v.Position.Z < lMin.Z) lMin.Z = v.Position.Z;
            if (v.Position.X > lMax.X) lMax.X = v.Position.X;
            if (v.Position.Y > lMax.Y) lMax.Y = v.Position.Y;
            if (v.Position.Z > lMax.Z) lMax.Z = v.Position.Z;
        }
        var bounds = new BoundingBox(lMin, lMax);
        bounds.Minimum.Y -= 0.5f;
        bounds.Maximum.Y += 0.5f;
        var sphere = BoundingSphere.FromBox(bounds);

        var props = LiquidProperties.Get(liquidMesh.Type);
        var color = new Color4(props.ColorR, props.ColorG, props.ColorB, props.Opacity);

        var entity = new Entity(entityName);
        var model = new Model();
        model.Meshes.Add(new Mesh
        {
            Draw = draw,
            BoundingBox = bounds,
            BoundingSphere = sphere,
        });
        model.Materials.Add(LiquidEffectFactory.CreateMaterial(
            GraphicsDevice, liquidMesh.Type, color, Quality));
        var modelComp = new ModelComponent(model) { IsShadowCaster = false };
        entity.Add(modelComp);
        entity.Transform.Position = chunkPos;

        Entity.AddChild(entity);
        _terrainEntities.Add(entity);
    }

    /// <summary>
    /// Renders trunk + canopy as a single entity with two sub-meshes so Stride
    /// treats the entire tree as one shadow-casting unit. This avoids shadow bias
    /// artifacts that cause the trunk shadow to detach from the canopy shadow.
    /// </summary>
    private void RenderTreeEntity(TreeChunkMeshData treeMeshes, Vector3 chunkPos, int cx, int cy)
    {
        bool hasTrunks = treeMeshes.Trunks.Vertices.Length > 0;
        bool hasCanopies = treeMeshes.Canopies.Vertices.Length > 0;
        if (!hasTrunks && !hasCanopies) return;

        var treeEntity = new Entity($"Trees_{cx}_{cy}");
        var model = new Model();

        var boundsMin = new Vector3(float.MaxValue);
        var boundsMax = new Vector3(float.MinValue);

        if (hasTrunks)
        {
            var (meshDraw, bb) = BuildMeshDraw(treeMeshes.Trunks);
            model.Meshes.Add(new Mesh
            {
                Draw = meshDraw,
                MaterialIndex = 0,
                BoundingBox = bb,
                BoundingSphere = BoundingSphere.FromBox(bb),
            });
            boundsMin = Vector3.Min(boundsMin, bb.Minimum);
            boundsMax = Vector3.Max(boundsMax, bb.Maximum);
        }

        // Trunk material (index 0)
        model.Materials.Add(CreateTerrainMaterial(new Color4(0.40f, 0.28f, 0.16f, 1f)));

        if (hasCanopies)
        {
            var (meshDraw, bb) = BuildMeshDraw(treeMeshes.Canopies);
            model.Meshes.Add(new Mesh
            {
                Draw = meshDraw,
                MaterialIndex = 1,
                BoundingBox = bb,
                BoundingSphere = BoundingSphere.FromBox(bb),
            });
            boundsMin = Vector3.Min(boundsMin, bb.Minimum);
            boundsMax = Vector3.Max(boundsMax, bb.Maximum);
        }

        // Canopy material (index 1)
        model.Materials.Add(CreateTerrainMaterial(new Color4(0.25f, 0.48f, 0.18f, 1f)));

        treeEntity.Add(new ModelComponent(model));
        treeEntity.Transform.Position = chunkPos;

        Entity.AddChild(treeEntity);
        _terrainEntities.Add(treeEntity);
    }

    private (MeshDraw draw, BoundingBox bounds) BuildMeshDraw(TreeMeshData treeMesh)
    {
        var verts = ConvertVertices(treeMesh.Vertices);
        var meshData = new GeometricMeshData<VertexPositionNormalTexture>(
            verts, treeMesh.Indices, true);
        var primitive = new GeometricPrimitive(GraphicsDevice, meshData);
        _primitives.Add(primitive);
        var draw = primitive.ToMeshDraw();

        var tMin = new Vector3(float.MaxValue);
        var tMax = new Vector3(float.MinValue);
        foreach (var v in verts)
        {
            if (v.Position.X < tMin.X) tMin.X = v.Position.X;
            if (v.Position.Y < tMin.Y) tMin.Y = v.Position.Y;
            if (v.Position.Z < tMin.Z) tMin.Z = v.Position.Z;
            if (v.Position.X > tMax.X) tMax.X = v.Position.X;
            if (v.Position.Y > tMax.Y) tMax.Y = v.Position.Y;
            if (v.Position.Z > tMax.Z) tMax.Z = v.Position.Z;
        }
        var bounds = new BoundingBox(tMin, tMax);
        bounds.Minimum.Y -= 0.5f;
        bounds.Maximum.Y += 3.0f;
        return (draw, bounds);
    }

    private static VertexPositionNormalTexture[] ConvertVertices(VertexData[] source)
    {
        var result = new VertexPositionNormalTexture[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var s = source[i];
            result[i] = new VertexPositionNormalTexture(
                new Vector3(s.Position.X, s.Position.Y, s.Position.Z),
                new Vector3(s.Normal.X, s.Normal.Y, s.Normal.Z),
                new Vector2(s.TexCoord.X, s.TexCoord.Y));
        }
        return result;
    }

    private Material CreateTerrainMaterial(Color4 color)
    {
        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor { Key = MaterialKeys.DiffuseValue })
            }
        };
        var material = Material.New(GraphicsDevice, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        return material;
    }

    private Material CreateEmissiveMaterial(Color4 color)
    {
        var diffuseColor = new ComputeColor { Key = MaterialKeys.DiffuseValue };
        var emissiveColor = new ComputeColor(new Stride.Core.Mathematics.Color4(color.R, color.G, color.B, 1f));
        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(diffuseColor),
                Emissive = new MaterialEmissiveMapFeature(emissiveColor)
                {
                    Intensity = new ComputeFloat(1.5f),
                }
            }
        };
        var material = Material.New(GraphicsDevice, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        return material;
    }

    private static Color4 GetDominantColor(TileData[,] tiles, int size)
    {
        // Count surface types and return the most common surface's color
        int countGrass = 0, countDirt = 0, countAsphalt = 0, countConcrete = 0;
        int countSand = 0, countMud = 0, countRock = 0, countMetal = 0;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                switch (tiles[x, y].Surface)
                {
                    case SurfaceType.Grass: countGrass++; break;
                    case SurfaceType.Dirt: countDirt++; break;
                    case SurfaceType.Asphalt: countAsphalt++; break;
                    case SurfaceType.Concrete: countConcrete++; break;
                    case SurfaceType.Sand: countSand++; break;
                    case SurfaceType.Mud: countMud++; break;
                    case SurfaceType.Rock: countRock++; break;
                    case SurfaceType.Metal: countMetal++; break;
                }
            }
        }

        // Find the max
        var counts = new (int count, Color4 color)[]
        {
            (countDirt,     new Color4(0.55f, 0.40f, 0.26f, 1f)),
            (countAsphalt,  new Color4(0.25f, 0.25f, 0.27f, 1f)),
            (countConcrete, new Color4(0.65f, 0.65f, 0.62f, 1f)),
            (countGrass,    new Color4(0.30f, 0.52f, 0.20f, 1f)),
            (countSand,     new Color4(0.80f, 0.72f, 0.50f, 1f)),
            (countMud,      new Color4(0.35f, 0.28f, 0.18f, 1f)),
            (countRock,     new Color4(0.50f, 0.48f, 0.42f, 1f)),
            (countMetal,    new Color4(0.45f, 0.47f, 0.50f, 1f)),
        };

        var best = counts[0];
        foreach (var c in counts)
            if (c.count > best.count)
                best = c;

        return best.color;
    }

    private static TileMapData CreateTileMapFromGrid(TileData[,] tiles, int size)
    {
        var map = new TileMapData(size, size);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.SetTileData(x, y, tiles[x, y]);
        return map;
    }

    private Entity CreateStructurePlaceholder(StructureEntry structure, Vector3 chunkPos)
    {
        // Determine size based on placement type
        bool isWall = structure.Placement is StructurePlacement.WallNorth
            or StructurePlacement.WallEast or StructurePlacement.WallSouth
            or StructurePlacement.WallWest;

        float sizeX = isWall ? HeightmapMeshGenerator.TileWorldSize : 1.0f;
        float sizeY = isWall ? 2.0f : 1.0f;
        float sizeZ = isWall ? 0.2f : 1.0f;

        var geom = GeometricPrimitive.Cube.New(GraphicsDevice, new Vector3(sizeX, sizeY, sizeZ));
        _primitives.Add(geom);
        var meshDraw = geom.ToMeshDraw();

        var halfExtent = new Vector3(sizeX, sizeY, sizeZ) * 0.5f;
        var bb = new BoundingBox(-halfExtent, halfExtent);
        var bs = BoundingSphere.FromBox(bb);

        var entity = new Entity($"Structure_{structure.StructureId}_{structure.Placement}");
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw, BoundingBox = bb, BoundingSphere = bs });
        model.Materials.Add(CreateTerrainMaterial(new Color4(0.45f, 0.42f, 0.38f, 1f)));
        entity.Add(new ModelComponent(model));

        // Position: chunk offset + local structure position + half height so base sits on ground
        entity.Transform.Position = chunkPos + new Vector3(
            structure.Position.X, structure.Position.Y + sizeY * 0.5f, structure.Position.Z);
        entity.Transform.Rotation = Quaternion.RotationY(structure.RotationY);

        return entity;
    }

    private void ClearTerrain()
    {
        foreach (var entity in _terrainEntities)
        {
            if (entity.Transform.Parent != null)
                entity.Transform.Parent.Entity.RemoveChild(entity);
            else
                entity.Scene?.Entities.Remove(entity);
        }
        _terrainEntities.Clear();
        foreach (var p in _primitives)
            p.Dispose();
        _primitives.Clear();
        _initialized = false;
    }

    public override void Cancel()
    {
        ClearTerrain();
    }
}

using Oravey2.Core.Rendering;
using Oravey2.Core.World.Rendering;
using Oravey2.Core.World.Terrain;
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

                var chunkData = new ChunkData(cx, cy,
                    CreateTileMapFromGrid(chunkTiles, chunkSize));

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

                // Build entity using the exact same pattern as working capsule/cube entities
                var meshEntity = new Entity("TerrainChunk");
                var model = new Model();
                model.Meshes.Add(new Mesh { Draw = meshDraw });
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


            }
        }

        _initialized = true;
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

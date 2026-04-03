using Oravey2.Core.World.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Oravey2.Core.World;

public class TileMapRendererScript : SyncScript
{
    public TileMapData? MapData { get; set; }
    public float TileSize { get; set; } = 1.0f;
    public float TileHeight { get; set; } = 0.1f;
    public float WallHeight { get; set; } = 1.0f;
    public float HeightStep { get; set; } = 0.25f;
    public QualitySettings Quality { get; set; } = QualitySettings.FromPreset(QualityPreset.Low);
    public BuildingRegistry? Buildings { get; set; }
    public IReadOnlyList<PropDefinition>? Props { get; set; }
    public int CurrentChunkX { get; set; }
    public int CurrentChunkY { get; set; }

    private readonly List<Entity> _tileEntities = new();
    private bool _initialized;

    public override void Start()
    {
        if (MapData != null)
            BuildMap();
    }

    public override void Update()
    {
        // Rebuild if map data changed
        if (MapData != null && !_initialized)
            BuildMap();
    }

    private void BuildMap()
    {
        ClearMap();

        if (MapData == null) return;

        if (Quality.SubTileAssembly)
            BuildSubTilePath();
        else
            BuildSimplePath();

        // Water pass (always runs, quality-independent)
        for (int x = 0; x < MapData.Width; x++)
        {
            for (int y = 0; y < MapData.Height; y++)
            {
                var tileData = MapData.GetTileData(x, y);
                if (!tileData.HasWater)
                    continue;

                var entity = CreateWaterEntity(x, y, tileData);
                Entity.AddChild(entity);
                _tileEntities.Add(entity);
            }
        }

        // Building pass
        if (Buildings != null)
        {
            foreach (var building in Buildings.GetByChunk(CurrentChunkX, CurrentChunkY))
            {
                var entity = CreateBuildingEntity(building);
                Entity.AddChild(entity);
                _tileEntities.Add(entity);
            }
        }

        // Prop pass
        if (Props != null)
        {
            foreach (var prop in Props)
            {
                if (prop.ChunkX != CurrentChunkX || prop.ChunkY != CurrentChunkY)
                    continue;
                var entity = CreatePropEntity(prop);
                Entity.AddChild(entity);
                _tileEntities.Add(entity);
            }
        }

        _initialized = true;
    }

    private void BuildSimplePath()
    {
        for (int x = 0; x < MapData!.Width; x++)
        {
            for (int y = 0; y < MapData.Height; y++)
            {
                var tileType = MapData.GetTile(x, y);
                if (tileType == TileType.Empty)
                    continue;

                var entity = CreateTileEntity(x, y, tileType);
                Entity.AddChild(entity);
                _tileEntities.Add(entity);
            }
        }
    }

    private void BuildSubTilePath()
    {
        for (int x = 0; x < MapData!.Width; x++)
        {
            for (int y = 0; y < MapData.Height; y++)
            {
                var tileData = MapData.GetTileData(x, y);
                if (tileData == TileData.Empty)
                    continue;

                var info = NeighborAnalyzer.GetNeighbors(MapData, x, y);

                foreach (var quadrant in new[] { Quadrant.NE, Quadrant.SE, Quadrant.SW, Quadrant.NW })
                {
                    var shape = NeighborAnalyzer.GetQuadrantShape(info, quadrant);
                    var config = SubTileSelector.GetSubTileConfig(shape, quadrant, tileData.Surface);
                    var entity = CreateSubTileEntity(x, y, tileData, config, quadrant);
                    Entity.AddChild(entity);
                    _tileEntities.Add(entity);
                }
            }
        }
    }

    private Entity CreateTileEntity(int x, int y, TileType tileType)
    {
        var tileData = MapData!.GetTileData(x, y);
        var baseHeight = tileData.HeightLevel * HeightStep;
        var meshHeight = tileType == TileType.Wall ? WallHeight : TileHeight;
        var color = GetTileColor(tileType);

        var entity = new Entity($"Tile_{x}_{y}");

        // Position: center of tile in world space, raised by height level
        var centerX = (x - MapData!.Width / 2f + 0.5f) * TileSize;
        var centerZ = (y - MapData.Height / 2f + 0.5f) * TileSize;
        entity.Transform.Position = new Vector3(centerX, baseHeight + meshHeight / 2f, centerZ);

        // Create procedural box mesh
        var meshDraw = GeometricPrimitive.Cube.New(GraphicsDevice, new Vector3(TileSize * 0.95f, meshHeight, TileSize * 0.95f)).ToMeshDraw();

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw });

        // Create material with the tile color
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
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));

        return entity;
    }

    private Entity CreateWaterEntity(int x, int y, TileData tileData)
    {
        var waterSurfaceY = WaterHelper.GetWaterSurfaceY(tileData);
        var waterColor = new Color4(0.1f, 0.3f, 0.5f, 0.6f); // Translucent blue

        var entity = new Entity($"Water_{x}_{y}");

        var centerX = (x - MapData!.Width / 2f + 0.5f) * TileSize;
        var centerZ = (y - MapData.Height / 2f + 0.5f) * TileSize;
        var waterThickness = 0.02f;
        entity.Transform.Position = new Vector3(centerX, waterSurfaceY, centerZ);

        // Flat quad for water surface — slightly larger for seamless connection
        var meshDraw = GeometricPrimitive.Cube.New(GraphicsDevice,
            new Vector3(TileSize * 0.98f, waterThickness, TileSize * 0.98f)).ToMeshDraw();

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw });

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
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, waterColor);
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));

        return entity;
    }

    private Entity CreateSubTileEntity(int x, int y, TileData tileData, SubTileConfig config, Quadrant quadrant)
    {
        var baseHeight = tileData.HeightLevel * HeightStep;
        var meshHeight = tileData.IsWalkable ? TileHeight : WallHeight;
        var color = GetSurfaceColor(tileData.Surface);

        var entity = new Entity($"SubTile_{x}_{y}_{quadrant}");

        // Half-tile size for sub-tile
        var halfTile = TileSize * 0.5f;
        var centerX = (x - MapData!.Width / 2f + 0.5f) * TileSize;
        var centerZ = (y - MapData.Height / 2f + 0.5f) * TileSize;

        // Offset quadrant position
        var (qx, qz) = quadrant switch
        {
            Quadrant.NE => (centerX + halfTile * 0.25f, centerZ - halfTile * 0.25f),
            Quadrant.SE => (centerX + halfTile * 0.25f, centerZ + halfTile * 0.25f),
            Quadrant.SW => (centerX - halfTile * 0.25f, centerZ + halfTile * 0.25f),
            Quadrant.NW => (centerX - halfTile * 0.25f, centerZ - halfTile * 0.25f),
            _ => (centerX, centerZ)
        };

        entity.Transform.Position = new Vector3((float)qx, baseHeight + meshHeight / 2f, (float)qz);
        entity.Transform.RotationEulerXYZ = new Vector3(0, MathF.PI * config.RotationDegrees / 180f, 0);

        var meshDraw = GeometricPrimitive.Cube.New(GraphicsDevice,
            new Vector3(halfTile * 0.95f, meshHeight, halfTile * 0.95f)).ToMeshDraw();

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw });

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
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));

        return entity;
    }

    private static Color4 GetSurfaceColor(SurfaceType surface) => surface switch
    {
        SurfaceType.Dirt => new Color4(0.45f, 0.38f, 0.28f, 1f),
        SurfaceType.Asphalt => new Color4(0.35f, 0.35f, 0.35f, 1f),
        SurfaceType.Concrete => new Color4(0.5f, 0.5f, 0.48f, 1f),
        SurfaceType.Grass => new Color4(0.3f, 0.45f, 0.2f, 1f),
        SurfaceType.Sand => new Color4(0.76f, 0.7f, 0.5f, 1f),
        SurfaceType.Mud => new Color4(0.35f, 0.28f, 0.2f, 1f),
        SurfaceType.Rock => new Color4(0.5f, 0.42f, 0.35f, 1f),
        SurfaceType.Metal => new Color4(0.55f, 0.55f, 0.6f, 1f),
        _ => new Color4(1f, 0f, 1f, 1f)
    };

    private Entity CreateBuildingEntity(BuildingDefinition building)
    {
        var entity = new Entity($"Building_{building.Id}");

        // Calculate center from footprint
        float avgX = (float)building.Footprint.Average(p => p.X);
        float avgY = (float)building.Footprint.Average(p => p.Y);
        var centerX = (avgX - MapData!.Width / 2f + 0.5f) * TileSize;
        var centerZ = (avgY - MapData.Height / 2f + 0.5f) * TileSize;

        // Use height of first footprint tile
        var (fx, fy) = building.Footprint[0];
        var baseHeight = MapData.GetTileData(fx, fy).HeightLevel * HeightStep;
        var buildingHeight = WallHeight * building.Floors;

        entity.Transform.Position = new Vector3(centerX, baseHeight + buildingHeight / 2f, centerZ);

        // Placeholder cube mesh sized to footprint
        int minX = building.Footprint.Min(p => p.X);
        int maxX = building.Footprint.Max(p => p.X);
        int minY = building.Footprint.Min(p => p.Y);
        int maxY = building.Footprint.Max(p => p.Y);
        float sizeX = (maxX - minX + 1) * TileSize * 0.9f;
        float sizeZ = (maxY - minY + 1) * TileSize * 0.9f;

        var meshDraw = GeometricPrimitive.Cube.New(GraphicsDevice,
            new Vector3(sizeX, buildingHeight, sizeZ)).ToMeshDraw();

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw });

        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor { Key = MaterialKeys.DiffuseValue })
            }
        };

        var color = new Color4(0.4f, 0.38f, 0.35f, 1f); // Building grey-brown
        var material = Material.New(GraphicsDevice, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));
        return entity;
    }

    private Entity CreatePropEntity(PropDefinition prop)
    {
        var entity = new Entity($"Prop_{prop.Id}");

        var centerX = (prop.LocalTileX - MapData!.Width / 2f + 0.5f) * TileSize;
        var centerZ = (prop.LocalTileY - MapData.Height / 2f + 0.5f) * TileSize;
        var baseHeight = MapData.GetTileData(prop.LocalTileX, prop.LocalTileY).HeightLevel * HeightStep;
        var propHeight = TileSize * prop.Scale;

        entity.Transform.Position = new Vector3(centerX, baseHeight + propHeight / 2f, centerZ);
        entity.Transform.RotationEulerXYZ = new Vector3(0, MathF.PI * prop.RotationDegrees / 180f, 0);
        entity.Transform.Scale = new Vector3(prop.Scale);

        var meshDraw = GeometricPrimitive.Cube.New(GraphicsDevice,
            new Vector3(TileSize * 0.5f, propHeight, TileSize * 0.5f)).ToMeshDraw();

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw });

        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor { Key = MaterialKeys.DiffuseValue })
            }
        };

        var color = new Color4(0.6f, 0.55f, 0.4f, 1f); // Prop tan
        var material = Material.New(GraphicsDevice, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));
        return entity;
    }

    private static Color4 GetTileColor(TileType type) => type switch
    {
        TileType.Ground => new Color4(0.45f, 0.38f, 0.28f, 1f),  // Dusty brown
        TileType.Road => new Color4(0.35f, 0.35f, 0.35f, 1f),     // Grey asphalt
        TileType.Rubble => new Color4(0.5f, 0.42f, 0.35f, 1f),    // Light brown rubble
        TileType.Water => new Color4(0.15f, 0.25f, 0.35f, 1f),    // Dark murky water
        TileType.Wall => new Color4(0.3f, 0.3f, 0.3f, 1f),        // Concrete grey
        _ => new Color4(1f, 0f, 1f, 1f)                             // Magenta = missing
    };

    public override void Cancel()
    {
        ClearMap();
    }

    private void ClearMap()
    {
        foreach (var entity in _tileEntities)
        {
            // Remove from parent (child entity model) or scene (legacy)
            if (entity.Transform.Parent != null)
                entity.Transform.Parent.Entity.RemoveChild(entity);
            else
                entity.Scene?.Entities.Remove(entity);
        }
        _tileEntities.Clear();
        _initialized = false;
    }
}

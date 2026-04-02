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

        for (int x = 0; x < MapData.Width; x++)
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

        _initialized = true;
    }

    private Entity CreateTileEntity(int x, int y, TileType tileType)
    {
        var height = tileType == TileType.Wall ? WallHeight : TileHeight;
        var color = GetTileColor(tileType);

        var entity = new Entity($"Tile_{x}_{y}");

        // Position: center of tile in world space
        var centerX = (x - MapData!.Width / 2f + 0.5f) * TileSize;
        var centerZ = (y - MapData.Height / 2f + 0.5f) * TileSize;
        entity.Transform.Position = new Vector3(centerX, height / 2f, centerZ);

        // Create procedural box mesh
        var meshDraw = GeometricPrimitive.Cube.New(GraphicsDevice, new Vector3(TileSize * 0.95f, height, TileSize * 0.95f)).ToMeshDraw();

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

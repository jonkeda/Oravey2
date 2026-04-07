using System.Numerics;

namespace Oravey2.Core.World.Vegetation;

/// <summary>
/// Describes a single tree instance within a chunk.
/// Position is in chunk-local world space (0–32 range per axis for a 16-tile chunk at 2m/tile).
/// </summary>
public sealed record TreeSpawn(
    Vector2 Position,
    TreeSpecies Species,
    byte GrowthStage,
    bool IsDead
);

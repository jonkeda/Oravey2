namespace Oravey2.Core.Camera;

/// <summary>
/// Discrete zoom/LOD levels for the multi-scale camera system.
/// </summary>
public enum ZoomLevel
{
    /// <summary>L1 — Local terrain view: individual tiles, entities, buildings.</summary>
    Local = 1,

    /// <summary>L2 — Regional map: biome splatting, town silhouettes, POI markers.</summary>
    Regional = 2,

    /// <summary>L3 — Continental strategic view: city dots, faction territories.</summary>
    Continental = 3,
}

namespace Oravey2.Core.UI;

/// <summary>
/// Configuration for the minimap renderer. Pure data — no Stride dependencies.
/// </summary>
public sealed class MinimapSettings
{
    /// <summary>Render resolution (square).</summary>
    public int Resolution { get; set; } = 256;

    /// <summary>Seconds between minimap refreshes.</summary>
    public float UpdateInterval { get; set; } = 0.5f;

    /// <summary>Minimap display opacity (0–1).</summary>
    public float Opacity { get; set; } = 0.85f;

    /// <summary>Whether fog of war is displayed.</summary>
    public bool ShowFogOfWar { get; set; } = true;

    /// <summary>Whether NPC dots are shown.</summary>
    public bool ShowNpcDots { get; set; } = true;

    /// <summary>Whether POI icons are shown.</summary>
    public bool ShowPoiIcons { get; set; } = true;

    /// <summary>Whether roads are drawn.</summary>
    public bool ShowRoads { get; set; } = true;

    /// <summary>Corner position on screen.</summary>
    public MinimapCorner Corner { get; set; } = MinimapCorner.BottomRight;

    /// <summary>Current view radius in world metres.</summary>
    public float ViewRadius { get; set; } = 100f;

    /// <summary>Minimum view radius (most zoomed in).</summary>
    public const float MinViewRadius = 50f;

    /// <summary>Maximum view radius (most zoomed out).</summary>
    public const float MaxViewRadius = 500f;

    /// <summary>Whether the minimap is in large overlay mode.</summary>
    public bool IsLargeMode { get; set; }

    /// <summary>Clamps <see cref="ViewRadius"/> to valid range.</summary>
    public void ClampViewRadius()
        => ViewRadius = Math.Clamp(ViewRadius, MinViewRadius, MaxViewRadius);

    /// <summary>Toggles between small corner and large overlay mode.</summary>
    public void ToggleSize() => IsLargeMode = !IsLargeMode;
}

/// <summary>Screen corner for minimap placement.</summary>
public enum MinimapCorner : byte
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

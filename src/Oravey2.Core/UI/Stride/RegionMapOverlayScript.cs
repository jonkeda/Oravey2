using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Oravey2.Core.Data;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.World;
using Color = Stride.Core.Mathematics.Color;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Full-screen region map overlay toggled by the M key.
/// Shows town locations (from POI data) and the player's current position.
/// </summary>
public class RegionMapOverlayScript : SyncScript
{
    public IInputProvider? InputProvider { get; set; }
    public GameStateManager? StateManager { get; set; }
    public SpriteFont? Font { get; set; }

    /// <summary>Region name displayed as the map title.</summary>
    public string RegionName { get; set; } = "";

    /// <summary>World dimensions in tiles (width).</summary>
    public int WorldTilesWide { get; set; }

    /// <summary>World dimensions in tiles (height).</summary>
    public int WorldTilesHigh { get; set; }

    /// <summary>Tile size in world units (meters).</summary>
    public float TileSize { get; set; } = 1f;

    /// <summary>POIs to display as markers on the map.</summary>
    public IReadOnlyList<PoiRecord>? Pois { get; set; }

    /// <summary>Delegate that returns the player's current world position.</summary>
    public Func<Vector3>? GetPlayerPosition { get; set; }

    /// <summary>Optional: provides HUD root element to hide while map is open.</summary>
    public Func<UIElement?>? GetHudRootElement { get; set; }

    /// <summary>Exposes overlay visibility for automation queries.</summary>
    public bool IsVisible => _visible;

    private UIComponent? _uiComponent;
    private Canvas? _mapCanvas;
    private TextBlock? _playerMarker;
    private bool _visible;

    // Layout constants
    private const float MapPadding = 40f;
    private const float CanvasWidth = 800f;
    private const float CanvasHeight = 600f;

    public override void Start()
    {
        base.Start();
        BuildUI();
    }

    public override void Update()
    {
        if (StateManager?.CurrentState is GameState.GameOver or GameState.InDialogue) return;

        if (InputProvider?.IsActionPressed(GameAction.OpenMap) == true)
            Toggle();

        if (_visible)
            UpdatePlayerMarker();
    }

    public void Toggle()
    {
        _visible = !_visible;
        if (_uiComponent != null)
        {
            if (_visible)
            {
                RefreshMap();
                _uiComponent.Page!.RootElement.Visibility = Visibility.Visible;
                if (GetHudRootElement?.Invoke() is { } hudOn)
                    hudOn.Visibility = Visibility.Collapsed;
            }
            else
            {
                _uiComponent.Page!.RootElement.Visibility = Visibility.Collapsed;
                if (GetHudRootElement?.Invoke() is { } hudOff)
                    hudOff.Visibility = Visibility.Visible;
            }
        }
    }

    private void BuildUI()
    {
        var title = new TextBlock
        {
            Text = $"Region Map: {RegionName}",
            Font = Font,
            TextSize = 22,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 5)
        };

        _mapCanvas = new Canvas
        {
            Width = CanvasWidth,
            Height = CanvasHeight,
            BackgroundColor = new Color(15, 15, 15, 255),
        };

        var legend = new TextBlock
        {
            Text = "(M) Close  \u2022  \u25A0 Town  \u2022  \u25C6 You",
            Font = Font,
            TextSize = 14,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 10)
        };

        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BackgroundColor = new Color(15, 15, 15, 255),
            Opacity = 1f,
            Children = { title, _mapCanvas, legend },
            Visibility = Visibility.Collapsed
        };

        var page = new UIPage { RootElement = container };
        _uiComponent = new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 };
        Entity.Add(_uiComponent);
    }

    private void RefreshMap()
    {
        if (_mapCanvas == null) return;

        _mapCanvas.Children.Clear();

        AddGridLines();
        AddPoiMarkers();
        AddPlayerMarker();
    }

    private void AddGridLines()
    {
        if (_mapCanvas == null) return;

        int chunksWide = WorldTilesWide / ChunkData.Size;
        int chunksHigh = WorldTilesHigh / ChunkData.Size;

        // Vertical lines
        for (int cx = 1; cx < chunksWide; cx++)
        {
            float x = (cx / (float)chunksWide) * CanvasWidth;
            var line = new Border
            {
                Width = 1,
                Height = CanvasHeight,
                BackgroundColor = new Color(60, 80, 60, 80),
            };
            line.DependencyProperties.Set(Canvas.PinOriginPropertyKey, new Vector3(0, 0, 0));
            line.DependencyProperties.Set(Canvas.AbsolutePositionPropertyKey, new Vector3(x, 0, 0));
            _mapCanvas.Children.Add(line);
        }

        // Horizontal lines
        for (int cy = 1; cy < chunksHigh; cy++)
        {
            float y = (cy / (float)chunksHigh) * CanvasHeight;
            var line = new Border
            {
                Width = CanvasWidth,
                Height = 1,
                BackgroundColor = new Color(60, 80, 60, 80),
            };
            line.DependencyProperties.Set(Canvas.PinOriginPropertyKey, new Vector3(0, 0, 0));
            line.DependencyProperties.Set(Canvas.AbsolutePositionPropertyKey, new Vector3(0, y, 0));
            _mapCanvas.Children.Add(line);
        }
    }

    private void AddPoiMarkers()
    {
        if (_mapCanvas == null || Pois == null) return;

        foreach (var poi in Pois)
        {
            // Skip zone POIs — they are town-internal areas, not map landmarks
            if (poi.Type.Equals("zone", StringComparison.OrdinalIgnoreCase))
                continue;

            var (mapX, mapY) = GridToMap(poi.GridX, poi.GridY);
            var (markerSize, labelSize, color) = GetMarkerStyle(poi.Type);

            // Marker dot
            var marker = new Border
            {
                Width = markerSize,
                Height = markerSize,
                BackgroundColor = color,
            };
            marker.DependencyProperties.Set(Canvas.PinOriginPropertyKey, new Vector3(0.5f, 0.5f, 0));
            marker.DependencyProperties.Set(Canvas.AbsolutePositionPropertyKey, new Vector3(mapX, mapY, 0));
            _mapCanvas.Children.Add(marker);

            // Label (skip for small POIs to reduce clutter)
            if (markerSize >= 8)
            {
                var label = new TextBlock
                {
                    Text = poi.Name,
                    Font = Font,
                    TextSize = labelSize,
                    TextColor = Color.White,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                label.DependencyProperties.Set(Canvas.PinOriginPropertyKey, new Vector3(0, 0.5f, 0));
                label.DependencyProperties.Set(Canvas.AbsolutePositionPropertyKey,
                    new Vector3(mapX + markerSize / 2f + 4, mapY, 0));
                _mapCanvas.Children.Add(label);
            }
        }
    }

    private void AddPlayerMarker()
    {
        if (_mapCanvas == null) return;

        _playerMarker = new TextBlock
        {
            Text = "\u25C6",
            Font = Font,
            TextSize = 20,
            TextColor = new Color(100, 255, 100),
        };
        _playerMarker.DependencyProperties.Set(Canvas.PinOriginPropertyKey, new Vector3(0.5f, 0.5f, 0));
        _mapCanvas.Children.Add(_playerMarker);

        UpdatePlayerMarker();
    }

    private void UpdatePlayerMarker()
    {
        if (_playerMarker == null || GetPlayerPosition == null) return;

        var pos = GetPlayerPosition();
        var (mapX, mapY) = WorldToMap(pos.X, pos.Z);
        _playerMarker.DependencyProperties.Set(Canvas.AbsolutePositionPropertyKey, new Vector3(mapX, mapY, 0));
    }

    /// <summary>
    /// Converts chunk grid coordinates to map canvas coordinates.
    /// POI GridX/GridY are chunk indices; we place the marker at the chunk center.
    /// </summary>
    internal (float MapX, float MapY) GridToMap(int gridX, int gridY)
    {
        int chunksWide = WorldTilesWide / ChunkData.Size;
        int chunksHigh = WorldTilesHigh / ChunkData.Size;

        if (chunksWide <= 0 || chunksHigh <= 0) return (0, 0);

        float mapX = ((gridX + 0.5f) / chunksWide) * CanvasWidth;
        float mapY = ((gridY + 0.5f) / chunksHigh) * CanvasHeight;
        return (mapX, mapY);
    }

    /// <summary>
    /// Converts world-space coordinates to map canvas coordinates.
    /// World origin is at the center of the terrain (negative half to positive half).
    /// </summary>
    internal (float MapX, float MapY) WorldToMap(float worldX, float worldZ)
    {
        float worldWidth = WorldTilesWide * TileSize;
        float worldDepth = WorldTilesHigh * TileSize;

        if (worldWidth <= 0 || worldDepth <= 0) return (0, 0);

        // World coords are centered: [-halfW, +halfW]
        float halfW = worldWidth / 2f;
        float halfD = worldDepth / 2f;

        float normX = (worldX + halfW) / worldWidth;
        float normY = (worldZ + halfD) / worldDepth;

        float mapX = normX * CanvasWidth;
        float mapY = normY * CanvasHeight;
        return (mapX, mapY);
    }

    /// <summary>
    /// Returns marker size, label size, and color based on POI type string.
    /// </summary>
    internal static (float MarkerSize, float LabelSize, Color Color) GetMarkerStyle(string poiType)
    {
        return poiType.ToLowerInvariant() switch
        {
            "metropolis" => (18, 18, new Color(255, 204, 68)),   // gold
            "city"       => (14, 16, new Color(221, 187, 102)),  // amber
            "town"       => (10, 14, new Color(204, 204, 136)),  // warm yellow
            "village"    => (8, 13, new Color(170, 187, 153)),   // light green
            "hamlet"     => (6, 12, new Color(136, 170, 136)),   // muted green
            _            => (8, 13, new Color(150, 150, 150)),   // gray fallback
        };
    }
}

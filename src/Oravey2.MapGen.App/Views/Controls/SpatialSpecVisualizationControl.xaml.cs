using Oravey2.MapGen.Generation;
using System.Numerics;

namespace Oravey2.MapGen.App.Views;

public partial class SpatialSpecVisualizationControl : ContentView
{
    private SpatialSpecDrawable? _drawable;
    private float _zoomLevel = 1.0f;
    private float _panX = 0.0f;
    private float _panY = 0.0f;

    // Color scheme
    private static readonly Color ColorGrass = Color.FromArgb("#90EE90");
    private static readonly Color ColorBuilding = Color.FromArgb("#808080");
    private static readonly Color ColorRoad = Color.FromArgb("#333333");
    private static readonly Color ColorWater = Color.FromArgb("#4169E1");
    private static readonly Color ColorGridLine = Color.FromArgb("#E0E0E0");

    public SpatialSpecVisualizationControl()
    {
        InitializeComponent();
        
        Canvas.Drawable = new SpatialSpecDrawable(this);
        _drawable = Canvas.Drawable as SpatialSpecDrawable;
    }

    /// Set the spatial transform to display
    public void SetSpatialTransform(TownSpatialTransform? transform)
    {
        if (_drawable != null)
        {
            _drawable.Transform = transform;
            _zoomLevel = 1.0f;
            _panX = 0.0f;
            _panY = 0.0f;
            Canvas.Invalidate();
        }
    }

    private void OnPinch(object sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Running)
        {
            _zoomLevel = Math.Clamp(_zoomLevel * (float)e.Scale, 0.1f, 10.0f);
            Canvas.Invalidate();
        }
    }

    private float _lastPanX, _lastPanY;

    private void OnDragStart(object sender, EventArgs e)
    {
        _lastPanX = _panX;
        _lastPanY = _panY;
    }

    private void OnDrag(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Running)
        {
            _panX = _lastPanX + (float)e.TotalX;
            _panY = _lastPanY + (float)e.TotalY;
            Canvas.Invalidate();
        }
    }

    private void OnDragEnd(object sender, EventArgs e)
    {
        // Pan complete
    }

    /// Custom drawable for rendering spatial spec
    private class SpatialSpecDrawable : IDrawable
    {
        private readonly SpatialSpecVisualizationControl _control;
        public TownSpatialTransform? Transform { get; set; }

        public SpatialSpecDrawable(SpatialSpecVisualizationControl control)
        {
            _control = control;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Transform == null)
            {
                DrawEmptyState(canvas, dirtyRect);
                return;
            }

            canvas.SaveState();

            // Draw background
            canvas.FillColor = ColorGrass;
            canvas.FillRectangle(dirtyRect);

            // Calculate cell size — zoom > 1 makes cells bigger
            var (gridWidth, gridHeight) = Transform.GetGridDimensions();
            float cellWidth = (dirtyRect.Width - 8) / gridWidth;
            float cellHeight = (dirtyRect.Height - 8) / gridHeight;
            float cellSize = Math.Min(cellWidth, cellHeight) * _control._zoomLevel;

            canvas.Translate(_control._panX + 4, _control._panY + 4);

            // Draw grid
            DrawGrid(canvas, gridWidth, gridHeight, cellSize);

            // Draw roads
            DrawRoads(canvas, cellSize);

            // Draw water
            DrawWater(canvas, cellSize);

            // Draw buildings
            DrawBuildings(canvas, cellSize);

            canvas.RestoreState();
        }

        private void DrawEmptyState(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            canvas.FontColor = Colors.Gray;
            canvas.FontSize = 14;
            canvas.DrawString("No spatial specification available",
                dirtyRect.Center.X, dirtyRect.Center.Y, HorizontalAlignment.Center);
        }

        private void DrawGrid(ICanvas canvas, int width, int height, float cellSize)
        {
            canvas.StrokeColor = ColorGridLine;
            canvas.StrokeSize = 1.0f;

            // Vertical lines
            for (int x = 0; x <= width; x++)
            {
                float lineX = x * cellSize;
                canvas.DrawLine(lineX, 0, lineX, height * cellSize);
            }

            // Horizontal lines
            for (int y = 0; y <= height; y++)
            {
                float lineY = y * cellSize;
                canvas.DrawLine(0, lineY, width * cellSize, lineY);
            }
        }

        private void DrawRoads(ICanvas canvas, float cellSize)
        {
            if (Transform == null)
                return;

            var roads = Transform.TransformRoadNetwork();
            canvas.StrokeColor = ColorRoad;
            canvas.StrokeSize = Math.Max(1.0f, 2.0f * _control._zoomLevel);

            foreach (var road in roads)
            {
                float x1 = road.From.X * cellSize;
                float y1 = road.From.Y * cellSize;
                float x2 = road.To.X * cellSize;
                float y2 = road.To.Y * cellSize;

                canvas.DrawLine(x1, y1, x2, y2);
            }
        }

        private void DrawWater(ICanvas canvas, float cellSize)
        {
            if (Transform == null)
                return;

            var waters = Transform.TransformWaterBodies();
            canvas.FillColor = ColorWater;

            foreach (var water in waters)
            {
                if (water.Polygon.Count < 3)
                    continue;

                // Draw water as rectangles around polygon bounds (simplified)
                var minX = water.Polygon.Min(v => v.X) * cellSize;
                var maxX = water.Polygon.Max(v => v.X) * cellSize;
                var minY = water.Polygon.Min(v => v.Y) * cellSize;
                var maxY = water.Polygon.Max(v => v.Y) * cellSize;

                canvas.FillRectangle(minX, minY, maxX - minX, maxY - minY);
            }
        }

        private void DrawBuildings(ICanvas canvas, float cellSize)
        {
            if (Transform == null)
                return;

            var buildings = Transform.TransformBuildingPlacements();
            canvas.FillColor = ColorBuilding;
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 1.0f;

            foreach (var (name, placement) in buildings)
            {
                float x = placement.CenterX * cellSize - (placement.WidthTiles * cellSize / 2.0f);
                float y = placement.CenterZ * cellSize - (placement.DepthTiles * cellSize / 2.0f);
                float width = placement.WidthTiles * cellSize;
                float depth = placement.DepthTiles * cellSize;

                // Draw rectangle (rotation not fully supported in simple MAUI canvas)
                canvas.FillRectangle(x, y, width, depth);
                canvas.DrawRectangle(x, y, width, depth);
            }
        }
    }
}

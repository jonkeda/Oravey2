using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.ViewModels.RegionTemplates;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.App.Views;

public class RegionTemplateMapDrawable : IDrawable
{
    // --- Data sources ---
    public float[,]? ElevationGrid { get; set; }
    public IReadOnlyList<TownItem>? Towns { get; set; }
    public IReadOnlyList<RoadItem>? Roads { get; set; }
    public IReadOnlyList<WaterItem>? WaterBodies { get; set; }

    // --- Bounds (lat/lon from preset) ---
    public double NorthLat { get; set; } = 53.0;
    public double SouthLat { get; set; } = 52.2;
    public double EastLon { get; set; } = 5.5;
    public double WestLon { get; set; } = 4.0;

    // --- Pan / Zoom ---
    public PointF Offset { get; set; }
    public float Zoom { get; set; } = 1.0f;

    // --- Layer visibility ---
    public bool ShowTowns { get; set; } = true;
    public bool ShowRoads { get; set; } = true;
    public bool ShowWater { get; set; } = true;
    public bool ShowLegend { get; set; } = true;

    // --- Geo/canvas conversion ---

    public PointF GeoToCanvas(double lat, double lon, float canvasWidth, float canvasHeight)
    {
        double latRange = NorthLat - SouthLat;
        double lonRange = EastLon - WestLon;
        if (latRange == 0 || lonRange == 0) return PointF.Zero;

        float x = (float)((lon - WestLon) / lonRange * canvasWidth);
        float y = (float)((NorthLat - lat) / latRange * canvasHeight); // Y-down

        return new PointF(x * Zoom + Offset.X, y * Zoom + Offset.Y);
    }

    public (double lat, double lon) CanvasToGeo(PointF point, float canvasWidth, float canvasHeight)
    {
        float x = (point.X - Offset.X) / Zoom;
        float y = (point.Y - Offset.Y) / Zoom;

        double latRange = NorthLat - SouthLat;
        double lonRange = EastLon - WestLon;

        double lon = WestLon + x / canvasWidth * lonRange;
        double lat = NorthLat - y / canvasHeight * latRange;
        return (lat, lon);
    }

    // --- Game-space to canvas (Vector2 nodes use game metres) ---

    private GeoMapper? _geoMapper;

    private GeoMapper GetGeoMapper()
    {
        _geoMapper ??= new GeoMapper(
            (NorthLat + SouthLat) / 2.0,
            (EastLon + WestLon) / 2.0);
        return _geoMapper;
    }

    public void ResetGeoMapper() => _geoMapper = null;

    private PointF GameToCanvas(Vector2 gamePos, float canvasWidth, float canvasHeight)
    {
        var mapper = GetGeoMapper();
        var (lat, lon) = mapper.GameXZToLatLon(gamePos.X, gamePos.Y);
        return GeoToCanvas(lat, lon, canvasWidth, canvasHeight);
    }

    // === DRAW ===

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        if (w <= 0 || h <= 0) return;

        // Background
        canvas.FillColor = Color.FromArgb("#1A1A2E");
        canvas.FillRectangle(dirtyRect);

        DrawElevation(canvas, w, h);
        if (ShowWater) DrawWater(canvas, w, h);
        if (ShowRoads) DrawRoads(canvas, w, h);
        if (ShowTowns) DrawTowns(canvas, w, h);
        if (ShowTowns) DrawLabels(canvas, w, h);
        if (ShowLegend) DrawLegend(canvas, w, h);
    }

    // --- Layer 1: Elevation ---

    private void DrawElevation(ICanvas canvas, float w, float h)
    {
        if (ElevationGrid is null) return;

        int rows = ElevationGrid.GetLength(0);
        int cols = ElevationGrid.GetLength(1);
        if (rows == 0 || cols == 0) return;

        // Find min/max
        float min = float.MaxValue, max = float.MinValue;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float v = ElevationGrid[r, c];
                if (float.IsNaN(v)) continue;
                if (v < min) min = v;
                if (v > max) max = v;
            }
        if (max <= min) return;

        // Downsample for performance — render ~200x200 blocks max
        int stepR = Math.Max(1, rows / 200);
        int stepC = Math.Max(1, cols / 200);
        float cellW = w * Zoom / (cols / stepC);
        float cellH = h * Zoom / (rows / stepR);

        for (int r = 0; r < rows; r += stepR)
            for (int c = 0; c < cols; c += stepC)
            {
                float v = ElevationGrid[r, c];
                if (float.IsNaN(v)) continue;

                float t = (v - min) / (max - min);
                canvas.FillColor = ElevationColor(t);

                float px = (float)c / cols * w * Zoom + Offset.X;
                float py = (float)r / rows * h * Zoom + Offset.Y;
                canvas.FillRectangle(px, py, cellW + 1, cellH + 1);
            }
    }

    private static Color ElevationColor(float t)
    {
        // deep blue → green → brown → white
        if (t < 0.1f) return Color.FromRgba(0.1f, 0.2f, 0.5f, 1f);        // sea/below
        if (t < 0.3f) return Color.FromRgba(0.2f, 0.5f + t, 0.2f, 1f);    // low green
        if (t < 0.6f) return Color.FromRgba(0.4f + t * 0.3f, 0.35f, 0.15f, 1f); // brown
        return Color.FromRgba(0.7f + t * 0.3f, 0.7f + t * 0.3f, 0.7f + t * 0.3f, 1f); // snow
    }

    // --- Layer 2: Water ---

    private void DrawWater(ICanvas canvas, float w, float h)
    {
        if (WaterBodies is null) return;

        foreach (var item in WaterBodies)
        {
            var body = item.Body;
            if (body.Geometry.Length < 2) continue;

            canvas.FillColor = item.IsIncluded
                ? Color.FromArgb("#4488CCAA")
                : Color.FromArgb("#44444466");
            canvas.StrokeColor = item.IsIncluded
                ? Color.FromArgb("#6699CCCC")
                : Color.FromArgb("#44444466");
            canvas.StrokeSize = 1;

            var path = new PathF();
            var first = GameToCanvas(body.Geometry[0], w, h);
            path.MoveTo(first);
            for (int i = 1; i < body.Geometry.Length; i++)
                path.LineTo(GameToCanvas(body.Geometry[i], w, h));
            path.Close();

            canvas.FillPath(path);
            canvas.DrawPath(path);
        }
    }

    // --- Layer 3: Roads ---

    private void DrawRoads(ICanvas canvas, float w, float h)
    {
        if (Roads is null) return;

        foreach (var item in Roads)
        {
            var seg = item.Segment;
            if (seg.Nodes.Length < 2) continue;

            canvas.StrokeColor = RoadColor(seg.RoadClass, item.IsIncluded);
            canvas.StrokeSize = RoadWidth(seg.RoadClass);

            var path = new PathF();
            var first = GameToCanvas(seg.Nodes[0], w, h);
            path.MoveTo(first);
            for (int i = 1; i < seg.Nodes.Length; i++)
                path.LineTo(GameToCanvas(seg.Nodes[i], w, h));

            canvas.DrawPath(path);
        }
    }

    private static Color RoadColor(LinearFeatureType cls, bool included)
    {
        float alpha = included ? 1f : 0.3f;
        return cls switch
        {
            LinearFeatureType.Motorway => Color.FromRgba(1f, 0.2f, 0.2f, alpha),
            LinearFeatureType.Trunk => Color.FromRgba(1f, 0.6f, 0.2f, alpha),
            LinearFeatureType.Primary => Color.FromRgba(1f, 1f, 0.3f, alpha),
            LinearFeatureType.Secondary => Color.FromRgba(1f, 1f, 1f, alpha),
            _ => Color.FromRgba(0.5f, 0.5f, 0.5f, alpha)
        };
    }

    private static float RoadWidth(LinearFeatureType cls) => cls switch
    {
        LinearFeatureType.Motorway => 3f,
        LinearFeatureType.Trunk => 2.5f,
        LinearFeatureType.Primary => 2f,
        LinearFeatureType.Secondary => 1.5f,
        _ => 1f
    };

    // --- Layer 4: Towns ---

    private void DrawTowns(ICanvas canvas, float w, float h)
    {
        if (Towns is null) return;

        foreach (var item in Towns)
        {
            var pos = GeoToCanvas(item.Lat, item.Lon, w, h);
            float radius = TownRadius(item.Category);

            // Selection ring
            if (item.IsSelected)
            {
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 2;
                canvas.DrawCircle(pos, radius + 3);
            }

            canvas.FillColor = item.IsIncluded ? Colors.White : Colors.Gray;
            if (!item.IsIncluded)
            {
                canvas.StrokeColor = Color.FromRgba(1f, 0.3f, 0.3f, 0.8f);
                canvas.StrokeSize = 1;
            }
            else
            {
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 0.5f;
            }

            canvas.FillCircle(pos, radius);
            canvas.DrawCircle(pos, radius);
        }
    }

    private static float TownRadius(TownCategory cat) => cat switch
    {
        TownCategory.Metropolis => 10f,
        TownCategory.City => 8f,
        TownCategory.Town => 6f,
        TownCategory.Village => 4f,
        _ => 3f // Hamlet
    };

    // --- Layer 5: Labels ---

    private void DrawLabels(ICanvas canvas, float w, float h)
    {
        if (Towns is null) return;

        foreach (var item in Towns)
        {
            // Zoom-dependent category filter
            if (Zoom < 0.5f && item.Category < TownCategory.City)
                continue;
            if (Zoom < 1.0f && item.Category < TownCategory.Town)
                continue;
            if (Zoom < 1.5f && item.Category < TownCategory.Village)
                continue;

            var pos = GeoToCanvas(item.Lat, item.Lon, w, h);
            float fontSize = item.Category switch
            {
                TownCategory.Metropolis => 14f,
                TownCategory.City => 12f,
                TownCategory.Town => 10f,
                _ => 8f
            };

            float textWidth = fontSize * item.Name.Length * 0.55f;
            float textHeight = fontSize + 2;
            float labelY = pos.Y - TownRadius(item.Category) - fontSize;

            // Background pill
            canvas.FillColor = Color.FromRgba(0, 0, 0, 0.6f);
            canvas.FillRoundedRectangle(
                pos.X - textWidth / 2 - 3, labelY - textHeight / 2 - 1,
                textWidth + 6, textHeight + 2, 3);

            // Shadow pass
            canvas.FontColor = Colors.Black;
            canvas.FontSize = fontSize;
            canvas.DrawString(item.Name, pos.X + 1, labelY + 1, HorizontalAlignment.Center);

            // Main pass
            canvas.FontColor = item.IsIncluded ? Colors.White : Colors.Gray;
            canvas.FontSize = fontSize;
            canvas.DrawString(item.Name, pos.X, labelY, HorizontalAlignment.Center);
        }
    }

    // --- Hit testing ---

    // --- Legend ---

    private void DrawLegend(ICanvas canvas, float w, float h)
    {
        const float pad = 8f;
        const float margin = 12f;
        const float swatchSize = 14f;
        const float rowHeight = 20f;
        const float labelFontSize = 11f;
        const float gradientBarWidth = 60f;

        var entries = new (Color? color, string label, bool isCircle, bool isGradient)[]
        {
            (null, "Low \u2192 High elevation", false, true),
            (Color.FromRgba(1f, 0.2f, 0.2f, 1f), "Motorway", false, false),
            (Color.FromRgba(1f, 0.6f, 0.2f, 1f), "Trunk", false, false),
            (Color.FromRgba(1f, 1f, 0.3f, 1f), "Primary", false, false),
            (Color.FromRgba(1f, 1f, 1f, 1f), "Secondary", false, false),
            (Color.FromRgba(0.5f, 0.5f, 0.5f, 1f), "Tertiary / Residential", false, false),
            (Color.FromArgb("#4488CC"), "Water", false, false),
            (Colors.White, "Town (included)", true, false),
            (Colors.Gray, "Town (excluded)", true, false),
        };

        float panelWidth = gradientBarWidth + 130f + pad * 2;
        float panelHeight = entries.Length * rowHeight + pad * 2;
        float px = margin;
        float py = h - panelHeight - margin;

        // Background
        canvas.FillColor = Color.FromRgba(0, 0, 0, 0.7f);
        canvas.FillRoundedRectangle(px, py, panelWidth, panelHeight, 6);

        float cy = py + pad;
        foreach (var (color, label, isCircle, isGradient) in entries)
        {
            float sx = px + pad;

            if (isGradient)
            {
                // Draw a mini elevation gradient bar
                float barW = gradientBarWidth;
                float steps = 10;
                float stepW = barW / steps;
                for (int i = 0; i < (int)steps; i++)
                {
                    float t = i / (steps - 1);
                    canvas.FillColor = ElevationColor(t);
                    canvas.FillRectangle(sx + i * stepW, cy + 2, stepW + 1, swatchSize - 4);
                }
            }
            else if (isCircle)
            {
                canvas.FillColor = color!;
                canvas.FillCircle(sx + swatchSize / 2, cy + swatchSize / 2, swatchSize / 2 - 1);
            }
            else
            {
                canvas.FillColor = color!;
                canvas.FillRectangle(sx, cy + 1, swatchSize, swatchSize - 2);
            }

            canvas.FontColor = Colors.White;
            canvas.FontSize = labelFontSize;
            canvas.DrawString(label, sx + swatchSize + 6, cy + swatchSize / 2 + 1, HorizontalAlignment.Left);

            cy += rowHeight;
        }
    }

    public TownItem? HitTestTown(PointF canvasPoint, float canvasWidth, float canvasHeight, float tolerance = 10f)
    {
        if (Towns is null) return null;

        TownItem? best = null;
        float bestDist = tolerance;

        foreach (var item in Towns)
        {
            var pos = GeoToCanvas(item.Lat, item.Lon, canvasWidth, canvasHeight);
            float dx = canvasPoint.X - pos.X;
            float dy = canvasPoint.Y - pos.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = item;
            }
        }
        return best;
    }

    public RoadItem? HitTestRoad(PointF canvasPoint, float canvasWidth, float canvasHeight, float tolerance = 5f)
    {
        if (Roads is null) return null;

        RoadItem? best = null;
        float bestDist = tolerance;

        foreach (var item in Roads)
        {
            var nodes = item.Segment.Nodes;
            for (int i = 1; i < nodes.Length; i++)
            {
                var a = GameToCanvas(nodes[i - 1], canvasWidth, canvasHeight);
                var b = GameToCanvas(nodes[i], canvasWidth, canvasHeight);
                float dist = DistanceToSegment(canvasPoint, a, b);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = item;
                }
            }
        }
        return best;
    }

    public WaterItem? HitTestWater(PointF canvasPoint, float canvasWidth, float canvasHeight)
    {
        if (WaterBodies is null) return null;

        foreach (var item in WaterBodies)
        {
            var geo = item.Body.Geometry;
            if (geo.Length < 3) continue;

            var points = new PointF[geo.Length];
            for (int i = 0; i < geo.Length; i++)
                points[i] = GameToCanvas(geo[i], canvasWidth, canvasHeight);

            if (PointInPolygon(canvasPoint, points))
                return item;
        }
        return null;
    }

    // --- Geometry helpers ---

    private static float DistanceToSegment(PointF p, PointF a, PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001f)
            return MathF.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0f, 1f);
        float projX = a.X + t * dx, projY = a.Y + t * dy;
        return MathF.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }

    private static bool PointInPolygon(PointF point, PointF[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                           (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                inside = !inside;
        }
        return inside;
    }
}

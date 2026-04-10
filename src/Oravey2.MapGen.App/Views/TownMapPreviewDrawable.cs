using Oravey2.MapGen.Generation;

namespace Oravey2.MapGen.App.Views;

/// <summary>
/// Custom drawable that renders a tile-map preview of a <see cref="TownMapResult"/>.
/// </summary>
public sealed class TownMapPreviewDrawable : IDrawable
{
    // Tile colour palette
    private static readonly Color GrassColour = Color.FromArgb("#3B5323");
    private static readonly Color DirtColour = Color.FromArgb("#8B7355");
    private static readonly Color RoadColour = Color.FromArgb("#6B6B6B");
    private static readonly Color WaterColour = Color.FromArgb("#1A3A5C");
    private static readonly Color BuildingColour = Color.FromArgb("#5A5A6A");
    private static readonly Color LandmarkColour = Color.FromArgb("#C8A84E");
    private static readonly Color PropColour = Color.FromArgb("#AA6644");
    private static readonly Color ZoneOutlineColour = Color.FromArgb("#80BBBBBB");
    private static readonly Color HazardFillColour = Color.FromArgb("#20FF0000");

    public TownMapResult? MapResult { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (MapResult is null)
        {
            canvas.FontColor = Colors.Gray;
            canvas.FontSize = 14;
            canvas.DrawString("No map generated yet.", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var layout = MapResult.Layout;
        var gridW = layout.Width;
        var gridH = layout.Height;
        if (gridW == 0 || gridH == 0) return;

        // Compute pixel-per-tile to fit the canvas
        var tileSize = Math.Min(dirtyRect.Width / gridW, dirtyRect.Height / gridH);
        var offsetX = (dirtyRect.Width - tileSize * gridW) / 2;
        var offsetY = (dirtyRect.Height - tileSize * gridH) / 2;

        // 1. Draw surface tiles
        for (var y = 0; y < gridH && y < layout.Surface.Length; y++)
        {
            var row = layout.Surface[y];
            for (var x = 0; x < gridW && x < row.Length; x++)
            {
                var colour = row[x] switch
                {
                    1 => DirtColour,
                    2 => RoadColour,
                    3 => WaterColour,
                    _ => GrassColour,
                };
                canvas.FillColor = colour;
                canvas.FillRectangle(offsetX + x * tileSize, offsetY + y * tileSize, tileSize, tileSize);
            }
        }

        // 2. Draw building footprints
        var isFirst = true;
        foreach (var b in MapResult.Buildings)
        {
            canvas.FillColor = isFirst ? LandmarkColour : BuildingColour;
            foreach (var tile in b.Footprint)
            {
                if (tile.Length < 2) continue;
                var tx = tile[0];
                var ty = tile[1];
                canvas.FillRectangle(offsetX + tx * tileSize, offsetY + ty * tileSize, tileSize, tileSize);
            }

            // Draw "L" on landmark
            if (isFirst && b.Footprint.Length > 0 && b.Footprint[0].Length >= 2)
            {
                canvas.FontColor = Colors.Black;
                canvas.FontSize = Math.Max(8, tileSize * 0.8f);
                var lx = offsetX + b.Footprint[0][0] * tileSize;
                var ly = offsetY + b.Footprint[0][1] * tileSize;
                canvas.DrawString("L", lx, ly, tileSize, tileSize, HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            isFirst = false;
        }

        // 3. Draw props as small dots
        foreach (var p in MapResult.Props)
        {
            var px = p.Placement.ChunkX * 16 + p.Placement.LocalTileX;
            var py = p.Placement.ChunkY * 16 + p.Placement.LocalTileY;
            canvas.FillColor = PropColour;
            var dotSize = Math.Max(2, tileSize * 0.4f);
            var dotOffset = (tileSize - dotSize) / 2;
            canvas.FillEllipse(offsetX + px * tileSize + dotOffset, offsetY + py * tileSize + dotOffset, dotSize, dotSize);
        }

        // 4. Draw zone outlines
        foreach (var z in MapResult.Zones)
        {
            var zx = z.ChunkStartX * 16 * tileSize + offsetX;
            var zy = z.ChunkStartY * 16 * tileSize + offsetY;
            var zw = (z.ChunkEndX - z.ChunkStartX + 1) * 16 * tileSize;
            var zh = (z.ChunkEndY - z.ChunkStartY + 1) * 16 * tileSize;

            // Hazard zones get a red fill overlay
            if (z.RadiationLevel > 0.05f)
            {
                canvas.FillColor = HazardFillColour;
                canvas.FillRectangle(zx, zy, zw, zh);
            }

            canvas.StrokeColor = ZoneOutlineColour;
            canvas.StrokeDashPattern = [4, 4];
            canvas.StrokeSize = 1;
            canvas.DrawRectangle(zx, zy, zw, zh);
        }
    }
}

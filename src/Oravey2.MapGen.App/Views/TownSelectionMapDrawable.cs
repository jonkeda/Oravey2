using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public sealed class TownSelectionMapDrawable : IDrawable
{
    private static readonly Color SurfaceColour = Color.FromArgb("#1C1B1F");
    private static readonly Color OutlineColour = Color.FromArgb("#938F99");
    private static readonly Color TextColour = Color.FromArgb("#E6E1E5");
    private static readonly Color ExcludedColour = Color.FromArgb("#60808080");

    public IReadOnlyList<TownSelectionItem>? Towns { get; set; }
    public double NorthLat { get; set; }
    public double SouthLat { get; set; }
    public double EastLon { get; set; }
    public double WestLon { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Background
        canvas.FillColor = SurfaceColour;
        canvas.FillRectangle(dirtyRect);

        if (Towns is null || Towns.Count == 0)
        {
            canvas.FontColor = Colors.Gray;
            canvas.FontSize = 14;
            canvas.DrawString("No towns yet.", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var latSpan = NorthLat - SouthLat;
        var lonSpan = EastLon - WestLon;
        if (latSpan <= 0 || lonSpan <= 0) return;

        // 5% padding on each edge
        var padX = dirtyRect.Width * 0.05f;
        var padY = dirtyRect.Height * 0.05f;
        var usableW = dirtyRect.Width - 2 * padX;
        var usableH = dirtyRect.Height - 2 * padY;

        float PxFromLon(double lon) => padX + (float)((lon - WestLon) / lonSpan) * usableW;
        float PyFromLat(double lat) => padY + (float)((NorthLat - lat) / latSpan) * usableH;

        // Region boundary (dashed rectangle)
        canvas.StrokeColor = OutlineColour;
        canvas.StrokeSize = 1;
        canvas.StrokeDashPattern = [6, 4];
        canvas.DrawRectangle(padX, padY, usableW, usableH);
        canvas.StrokeDashPattern = null;

        // Town dots
        for (var i = 0; i < Towns.Count; i++)
        {
            var town = Towns[i];
            var px = PxFromLon(town.Longitude);
            var py = PyFromLat(town.Latitude);

            var radius = RadiusFromSize(town.Size);

            if (town.IsIncluded)
            {
                canvas.FillColor = Color.FromArgb(town.DestructionColor);
            }
            else
            {
                canvas.FillColor = ExcludedColour;
            }

            canvas.FillCircle(px, py, radius);

            // Outline
            canvas.StrokeColor = TextColour;
            canvas.StrokeSize = 1;
            canvas.DrawCircle(px, py, radius);

            // Label — alternate above/below to reduce overlap
            canvas.FontColor = TextColour;
            canvas.FontSize = 10;
            var labelY = i % 2 == 0 ? py + radius + 2 : py - radius - 12;
            var labelRect = new RectF(px - 60, labelY, 120, 14);
            canvas.DrawString(town.GameName, labelRect, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static float RadiusFromSize(TownCategory size) => size switch
    {
        TownCategory.Hamlet => 4f,
        TownCategory.Village => 6f,
        TownCategory.Town => 8f,
        TownCategory.City => 10f,
        TownCategory.Metropolis => 14f,
        _ => 6f,
    };
}

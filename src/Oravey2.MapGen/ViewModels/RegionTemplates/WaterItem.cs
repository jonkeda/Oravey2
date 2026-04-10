using System.Numerics;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels.RegionTemplates;

public class WaterItem : ViewModelBase
{
    public WaterBody Body { get; }

    private bool _isIncluded = true;
    public bool IsIncluded { get => _isIncluded; set => SetProperty(ref _isIncluded, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public string Name => Body.Type.ToString();
    public string Type => Body.Type.ToString();

    public double AreaKm2
    {
        get
        {
            if (Body.Type == WaterType.River || Body.Type == WaterType.Canal)
                return 0;
            return Math.Abs(ComputePolygonArea(Body.Geometry)) / 1_000_000.0;
        }
    }

    public WaterItem(WaterBody body) => Body = body;

    private static double ComputePolygonArea(Vector2[] polygon)
    {
        if (polygon.Length < 3) return 0;
        double area = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area / 2.0;
    }
}

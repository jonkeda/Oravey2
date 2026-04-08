using System.Numerics;
using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.MapGen.ViewModels.WorldTemplate;

public class RoadItem : ViewModelBase
{
    public RoadSegment Segment { get; }

    private bool _isIncluded = true;
    public bool IsIncluded { get => _isIncluded; set => SetProperty(ref _isIncluded, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private string _nearTown = string.Empty;
    public string NearTown { get => _nearTown; set => SetProperty(ref _nearTown, value); }

    public string Classification => Segment.RoadClass.ToString();
    public int PointCount => Segment.Nodes.Length;

    public double LengthKm
    {
        get
        {
            double total = 0;
            var nodes = Segment.Nodes;
            for (int i = 1; i < nodes.Length; i++)
                total += Vector2.Distance(nodes[i - 1], nodes[i]);
            return total / 1000.0;
        }
    }

    public RoadItem(RoadSegment segment) => Segment = segment;
}

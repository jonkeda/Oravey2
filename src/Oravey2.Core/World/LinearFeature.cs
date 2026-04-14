using System.Numerics;

namespace Oravey2.Core.World;

public sealed class LinearFeatureNode
{
    public Vector2 Position { get; set; }
    public float? OverrideHeight { get; set; }
}

public sealed class LinearFeature
{
    public LinearFeatureType Type { get; set; }
    public string Style { get; set; } = "";
    public float Width { get; set; }
    public IReadOnlyList<LinearFeatureNode> Nodes { get; set; } = [];
}

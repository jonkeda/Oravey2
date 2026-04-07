using System.Numerics;

namespace Oravey2.Core.World;

public sealed record LinearFeatureNode(Vector2 Position, float? OverrideHeight = null);

public sealed record LinearFeature(
    LinearFeatureType Type,
    string Style,
    float Width,
    IReadOnlyList<LinearFeatureNode> Nodes);

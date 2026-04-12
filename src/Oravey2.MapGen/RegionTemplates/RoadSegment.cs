using System.Numerics;
using Oravey2.Core.World;

namespace Oravey2.MapGen.RegionTemplates;

public record RoadSegment(LinearFeatureType RoadClass, Vector2[] Nodes);

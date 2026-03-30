namespace Oravey2.Core.AI.Pathfinding;

public sealed record PathResult(bool Found, List<(int X, int Y)> Path);

public interface IPathfinder
{
    PathResult FindPath(int startX, int startY, int goalX, int goalY,
                        World.TileMapData map);
}

using Oravey2.Core.World;

namespace Oravey2.Core.AI.Pathfinding;

public sealed class TileGridPathfinder : IPathfinder
{
    private static readonly (int DX, int DY, float Cost)[] Neighbors =
    [
        (-1, 0, 1f), (1, 0, 1f), (0, -1, 1f), (0, 1, 1f),
        (-1, -1, 1.414f), (-1, 1, 1.414f), (1, -1, 1.414f), (1, 1, 1.414f)
    ];

    public PathResult FindPath(int startX, int startY, int goalX, int goalY,
                               TileMapData map)
    {
        if (startX == goalX && startY == goalY)
            return new PathResult(true, [(startX, startY)]);

        if (!IsWalkable(map.GetTile(startX, startY)) || !IsWalkable(map.GetTile(goalX, goalY)))
            return new PathResult(false, []);

        var open = new PriorityQueue<(int X, int Y), float>();
        var gScore = new Dictionary<(int, int), float>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();

        var start = (startX, startY);
        var goal = (goalX, goalY);

        gScore[start] = 0;
        open.Enqueue((startX, startY), Heuristic(startX, startY, goalX, goalY));

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == (goalX, goalY))
                return new PathResult(true, ReconstructPath(cameFrom, goal));

            var currentG = gScore[current];

            foreach (var (dx, dy, cost) in Neighbors)
            {
                var nx = current.X + dx;
                var ny = current.Y + dy;

                if (nx < 0 || nx >= map.Width || ny < 0 || ny >= map.Height)
                    continue;
                if (!IsWalkable(map.GetTile(nx, ny)))
                    continue;

                if (dx != 0 && dy != 0)
                {
                    if (!IsWalkable(map.GetTile(current.X + dx, current.Y)) ||
                        !IsWalkable(map.GetTile(current.X, current.Y + dy)))
                        continue;
                }

                // Height-based passability check
                var heightDelta = HeightHelper.GetHeightDelta(map, current.X, current.Y, nx, ny);
                if (!HeightHelper.IsPassable(heightDelta))
                    continue;

                var moveCost = cost;
                if (map.GetTile(nx, ny) == TileType.Rubble)
                    moveCost *= 2f;

                // Apply slope movement cost
                moveCost *= HeightHelper.GetSlopeMovementCost(heightDelta);

                var tentativeG = currentG + moveCost;
                var neighbor = (nx, ny);

                if (!gScore.TryGetValue(neighbor, out var existingG) || tentativeG < existingG)
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    var f = tentativeG + Heuristic(nx, ny, goalX, goalY);
                    open.Enqueue((nx, ny), f);
                }
            }
        }

        return new PathResult(false, []);
    }

    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        var dx = Math.Abs(ax - bx);
        var dy = Math.Abs(ay - by);
        return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy);
    }

    private static List<(int X, int Y)> ReconstructPath(
        Dictionary<(int, int), (int, int)> cameFrom, (int, int) current)
    {
        var path = new List<(int X, int Y)> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    public static bool IsWalkable(TileType tile) => tile switch
    {
        TileType.Ground => true,
        TileType.Road => true,
        TileType.Rubble => true,
        _ => false
    };
}

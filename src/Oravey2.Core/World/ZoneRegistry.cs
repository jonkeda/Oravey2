namespace Oravey2.Core.World;

/// <summary>
/// Stores all zone definitions and provides lookup by chunk coordinates.
/// </summary>
public sealed class ZoneRegistry
{
    private readonly List<ZoneDefinition> _zones = new();

    public IReadOnlyList<ZoneDefinition> Zones => _zones;

    public void Register(ZoneDefinition zone)
    {
        if (_zones.Any(z => z.Id == zone.Id))
            throw new InvalidOperationException($"Zone '{zone.Id}' is already registered.");
        _zones.Add(zone);
    }

    /// <summary>
    /// Finds the zone that contains the given chunk coordinates, or null if no zone matches.
    /// </summary>
    public ZoneDefinition? GetZoneForChunk(int cx, int cy)
        => _zones.FirstOrDefault(z => z.ContainsChunk(cx, cy));

    /// <summary>
    /// Gets a zone by its ID, or null if not found.
    /// </summary>
    public ZoneDefinition? GetZone(string zoneId)
        => _zones.FirstOrDefault(z => z.Id == zoneId);

    /// <summary>
    /// Returns all zones that are valid fast-travel targets.
    /// </summary>
    public IEnumerable<ZoneDefinition> GetFastTravelZones()
        => _zones.Where(z => z.IsFastTravelTarget);
}

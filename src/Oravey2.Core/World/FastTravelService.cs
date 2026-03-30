using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.World;

/// <summary>
/// Manages discovered fast-travel locations, validates travel, and calculates time costs.
/// </summary>
public sealed class FastTravelService
{
    /// <summary>Distance divisor to get in-game hours cost.</summary>
    public const float TravelTimeDivisor = 10f;

    private readonly Dictionary<string, DiscoveredLocation> _locations = new();
    private readonly IEventBus _eventBus;

    public IReadOnlyList<DiscoveredLocation> Locations
        => _locations.Values.ToList().AsReadOnly();

    public FastTravelService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Discovers a new location. Publishes LocationDiscoveredEvent if new.
    /// </summary>
    public bool Discover(DiscoveredLocation location)
    {
        if (_locations.ContainsKey(location.Id))
            return false;

        _locations[location.Id] = location;
        _eventBus.Publish(new LocationDiscoveredEvent(location.Id, location.Name));
        return true;
    }

    /// <summary>
    /// Checks if a location has been discovered.
    /// </summary>
    public bool IsDiscovered(string locationId)
        => _locations.ContainsKey(locationId);

    /// <summary>
    /// Gets a discovered location by ID, or null.
    /// </summary>
    public DiscoveredLocation? GetLocation(string locationId)
        => _locations.TryGetValue(locationId, out var loc) ? loc : null;

    /// <summary>
    /// Checks whether the player can travel between two discovered locations.
    /// Both must be discovered.
    /// </summary>
    public bool CanTravel(string fromId, string toId)
    {
        if (fromId == toId) return false;
        return _locations.ContainsKey(fromId) && _locations.ContainsKey(toId);
    }

    /// <summary>
    /// Calculates travel time in in-game hours based on chunk distance.
    /// Formula: Manhattan distance of chunks / TravelTimeDivisor.
    /// Returns -1 if either location is not discovered.
    /// </summary>
    public float GetTravelTime(string fromId, string toId)
    {
        if (!_locations.TryGetValue(fromId, out var from) ||
            !_locations.TryGetValue(toId, out var to))
            return -1f;

        float distance = Math.Abs(to.ChunkX - from.ChunkX) + Math.Abs(to.ChunkY - from.ChunkY);
        return distance / TravelTimeDivisor;
    }

    /// <summary>
    /// Executes fast travel. Publishes FastTravelEvent with the time cost.
    /// Returns the destination location, or null if travel is not possible.
    /// The caller (Stride script) handles actual teleportation and time advance.
    /// </summary>
    public (DiscoveredLocation destination, float hoursCost)? Travel(string fromId, string toId)
    {
        if (!CanTravel(fromId, toId))
            return null;

        var destination = _locations[toId];
        float hours = GetTravelTime(fromId, toId);

        _eventBus.Publish(new FastTravelEvent(fromId, toId, hours));
        return (destination, hours);
    }
}

using Oravey2.Core.World;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of map data for the world map screen: discovered locations and fast-travel state.
/// </summary>
public sealed record MapLocationView(
    string Id,
    string Name,
    int ChunkX,
    int ChunkY,
    bool CanTravelTo
);

public sealed record MapViewModel(
    IReadOnlyList<MapLocationView> Locations,
    string? CurrentLocationId,
    float InGameHour,
    DayPhase Phase
)
{
    /// <summary>
    /// Builds map view from fast-travel service and day/night state.
    /// </summary>
    public static MapViewModel Create(
        FastTravelService fastTravel,
        DayNightCycleProcessor dayNight,
        string? currentLocationId)
    {
        var locations = fastTravel.Locations.Select(loc => new MapLocationView(
            Id: loc.Id,
            Name: loc.Name,
            ChunkX: loc.ChunkX,
            ChunkY: loc.ChunkY,
            CanTravelTo: currentLocationId != null && fastTravel.CanTravel(currentLocationId, loc.Id)
        )).ToList();

        return new MapViewModel(
            Locations: locations,
            CurrentLocationId: currentLocationId,
            InGameHour: dayNight.InGameHour,
            Phase: dayNight.CurrentPhase
        );
    }
}

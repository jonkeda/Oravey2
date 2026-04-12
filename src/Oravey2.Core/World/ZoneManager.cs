using Microsoft.Extensions.Logging;
using Oravey2.Core.Bootstrap;
using Oravey2.Core.Data;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

namespace Oravey2.Core.World;

/// <summary>
/// Tracks the current zone and orchestrates zone transitions via RegionLoader.
/// </summary>
public class ZoneManager
{
    private readonly ScenarioLoader _scenarioLoader;

    public RegionLoader? RegionLoader { get; set; }
    public SaveStateStore? SaveStateStore { get; set; }

    public string? CurrentZoneId { get; private set; }

    public string CurrentZoneName => CurrentZoneId switch
    {
        "town" => "Haven",
        "wasteland" => "Wasteland",
        _ => CurrentZoneId ?? "Unknown",
    };

    public ZoneManager(ScenarioLoader scenarioLoader)
    {
        _scenarioLoader = scenarioLoader;
    }

    public void SetCurrentZone(string zoneId)
    {
        CurrentZoneId = zoneId;
    }

    public void TransitionTo(string zoneId, Scene rootScene, IGame game,
        Entity cameraEntity, GameStateManager gsm, IEventBus eventBus,
        IInputProvider input, ILogger logger, Vector3 playerSpawn)
    {
        if (RegionLoader == null)
            throw new InvalidOperationException("RegionLoader is not set on ZoneManager.");

        _scenarioLoader.Unload(rootScene);

        RegionLoader.LoadRegion(zoneId, rootScene, (Game)game, cameraEntity, gsm, eventBus, input, logger, playerSpawn);
        _scenarioLoader.SyncFromRegion(RegionLoader);

        CurrentZoneId = zoneId;
    }

    /// <summary>
    /// Transitions between database-driven regions via RegionLoader.
    /// Saves/restores per-region player positions through SaveStateStore.
    /// </summary>
    public void TransitionToRegion(string regionName, Scene rootScene, Game game,
        Entity cameraEntity, GameStateManager gsm, IEventBus eventBus,
        IInputProvider input, ILogger logger, string? spawnPoi = null)
    {
        if (RegionLoader == null)
            throw new InvalidOperationException("RegionLoader is not set on ZoneManager.");

        // Save current player position before leaving
        if (SaveStateStore != null && RegionLoader.PlayerEntity != null && RegionLoader.CurrentRegionName != null)
        {
            var pos = RegionLoader.PlayerEntity.Transform.Position;
            SaveStateStore.SavePlayerPosition(RegionLoader.CurrentRegionName, pos.X, pos.Y, pos.Z);
        }

        RegionLoader.UnloadCurrentRegion(rootScene);

        // Try to restore saved position for target region
        Vector3? spawnOverride = null;
        if (SaveStateStore != null)
        {
            var savedPos = SaveStateStore.GetPlayerPosition(regionName);
            if (savedPos.HasValue)
                spawnOverride = new Vector3(savedPos.Value.X, savedPos.Value.Y, savedPos.Value.Z);
        }

        RegionLoader.LoadRegion(regionName, rootScene, game,
            cameraEntity, gsm, eventBus, input, logger, spawnOverride);

        // Persist which region the player is now in
        SaveStateStore?.SetCurrentRegion(regionName);
        CurrentZoneId = regionName;
    }
}

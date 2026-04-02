using Microsoft.Extensions.Logging;
using Oravey2.Core.Bootstrap;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

namespace Oravey2.Core.World;

/// <summary>
/// Tracks the current zone and orchestrates zone transitions via ScenarioLoader.
/// </summary>
public class ZoneManager
{
    private readonly ScenarioLoader _scenarioLoader;

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
        _scenarioLoader.Unload(rootScene);

        var scenarioId = zoneId switch
        {
            "town" => "town",
            "wasteland" => "wasteland",
            _ => zoneId,
        };

        _scenarioLoader.Load(scenarioId, rootScene, (Game)game, cameraEntity, gsm, eventBus, input, logger);

        if (_scenarioLoader.PlayerEntity != null)
            _scenarioLoader.PlayerEntity.Transform.Position = playerSpawn;

        CurrentZoneId = zoneId;
    }
}

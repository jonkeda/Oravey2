using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Camera;

/// <summary>
/// Maps camera altitude to a discrete <see cref="ZoomLevel"/> with smooth crossfade
/// transitions. Pure logic — no Stride dependencies.
///
/// Transition zones:
///   L1 ↔ L2:  30 m – 50 m
///   L2 ↔ L3: 400 m – 600 m
/// </summary>
public sealed class ZoomLevelController
{
    // --- L1 ↔ L2 transition ---
    public const float L1L2LowerBound = 30f;
    public const float L1L2UpperBound = 50f;

    // --- L2 ↔ L3 transition ---
    public const float L2L3LowerBound = 400f;
    public const float L2L3UpperBound = 600f;

    private readonly IEventBus _eventBus;
    private ZoomLevel _currentLevel = ZoomLevel.Local;

    /// <summary>Current discrete zoom level.</summary>
    public ZoomLevel CurrentLevel => _currentLevel;

    /// <summary>
    /// Blend alpha during a crossfade transition (0 → 1).
    /// 0 = fully in the lower level, 1 = fully in the upper level.
    /// Outside a transition zone this is 0 (fully in current level).
    /// </summary>
    public float TransitionAlpha { get; private set; }

    public ZoomLevelController(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Updates the zoom level and transition alpha based on the current camera altitude (Y position).
    /// </summary>
    public void Update(float cameraAltitude)
    {
        ZoomLevel newLevel;
        float alpha;

        if (cameraAltitude < L1L2LowerBound)
        {
            // Fully L1
            newLevel = ZoomLevel.Local;
            alpha = 0f;
        }
        else if (cameraAltitude < L1L2UpperBound)
        {
            // Transitioning L1 → L2
            newLevel = ZoomLevel.Local;
            alpha = (cameraAltitude - L1L2LowerBound) / (L1L2UpperBound - L1L2LowerBound);
        }
        else if (cameraAltitude < L2L3LowerBound)
        {
            // Fully L2
            newLevel = ZoomLevel.Regional;
            alpha = 0f;
        }
        else if (cameraAltitude < L2L3UpperBound)
        {
            // Transitioning L2 → L3
            newLevel = ZoomLevel.Regional;
            alpha = (cameraAltitude - L2L3LowerBound) / (L2L3UpperBound - L2L3LowerBound);
        }
        else
        {
            // Fully L3
            newLevel = ZoomLevel.Continental;
            alpha = 0f;
        }

        TransitionAlpha = alpha;

        if (newLevel != _currentLevel)
        {
            var old = _currentLevel;
            _currentLevel = newLevel;
            _eventBus.Publish(new ZoomLevelChangedEvent(old, newLevel));
        }
    }

    /// <summary>
    /// Returns the time scale multiplier for the current zoom level.
    /// L1 = 1×, L2 = 60×, L3 = 1440×.
    /// </summary>
    public static float GetTimeScale(ZoomLevel level) => level switch
    {
        ZoomLevel.Local => 1f,
        ZoomLevel.Regional => 60f,
        ZoomLevel.Continental => 1440f,
        _ => 1f,
    };
}

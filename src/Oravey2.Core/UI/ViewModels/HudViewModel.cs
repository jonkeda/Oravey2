using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Combat;
using Oravey2.Core.Survival;
using Oravey2.Core.World;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Read-only snapshot of all data the HUD needs to display.
/// Computed from live game components — no mutable state.
/// </summary>
public sealed record HudViewModel(
    int CurrentHP,
    int MaxHP,
    float CurrentAP,
    int MaxAP,
    int Level,
    int CurrentXP,
    int XPToNextLevel,
    float InGameHour,
    DayPhase Phase,
    string? CurrentZoneName,
    float Hunger,
    float Thirst,
    float Fatigue,
    int RadiationLevel,
    bool SurvivalEnabled,
    IReadOnlyList<string?> QuickSlots
)
{
    /// <summary>
    /// Builds a HUD snapshot from live game components.
    /// </summary>
    public static HudViewModel Create(
        HealthComponent health,
        CombatComponent combat,
        LevelComponent level,
        DayNightCycleProcessor dayNight,
        string? currentZoneName,
        SurvivalComponent? survival,
        RadiationComponent? radiation,
        QuickSlotBar quickSlots)
    {
        return new HudViewModel(
            CurrentHP: health.CurrentHP,
            MaxHP: health.MaxHP,
            CurrentAP: combat.CurrentAP,
            MaxAP: combat.MaxAP,
            Level: level.Level,
            CurrentXP: level.CurrentXP,
            XPToNextLevel: level.XPToNextLevel,
            InGameHour: dayNight.InGameHour,
            Phase: dayNight.CurrentPhase,
            CurrentZoneName: currentZoneName,
            Hunger: survival?.Hunger ?? 0f,
            Thirst: survival?.Thirst ?? 0f,
            Fatigue: survival?.Fatigue ?? 0f,
            RadiationLevel: radiation?.Level ?? 0,
            SurvivalEnabled: survival?.Enabled ?? false,
            QuickSlots: quickSlots.GetAllSlots()
        );
    }
}

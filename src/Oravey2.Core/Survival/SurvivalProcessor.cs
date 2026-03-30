namespace Oravey2.Core.Survival;

using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

public sealed class SurvivalProcessor
{
    public const float HungerDecayRate = 2.0f;
    public const float ThirstDecayRate = 3.0f;
    public const float FatigueDecayRate = 1.5f;

    public const int StarvingHPDrain = 2;
    public const int DehydratedHPDrain = 3;

    public const string HungerBuffSource = "Survival_HungerBuff";
    public const string HungerDebuffSource = "Survival_HungerDebuff";
    public const string ThirstBuffSource = "Survival_ThirstBuff";
    public const string ThirstDebuffSource = "Survival_ThirstDebuff";
    public const string FatigueBuffSource = "Survival_FatigueBuff";
    public const string FatigueDebuffSource = "Survival_FatigueDebuff";

    private readonly IEventBus _eventBus;

    public SurvivalProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Tick(
        SurvivalComponent survival,
        StatsComponent stats,
        HealthComponent health,
        float deltaHours)
    {
        if (!survival.Enabled) return;

        survival.Hunger += HungerDecayRate * deltaHours;
        survival.Thirst += ThirstDecayRate * deltaHours;
        survival.Fatigue += FatigueDecayRate * deltaHours;
        survival.Clamp();

        ApplyHungerEffects(survival, stats);
        ApplyThirstEffects(survival, stats);
        ApplyFatigueEffects(survival, stats);

        var deltaMinutes = deltaHours * 60f;
        if (SurvivalComponent.GetThreshold(survival.Hunger) == SurvivalThreshold.Critical)
            health.TakeDamage((int)(StarvingHPDrain * deltaMinutes));

        if (SurvivalComponent.GetThreshold(survival.Thirst) == SurvivalThreshold.Critical)
            health.TakeDamage((int)(DehydratedHPDrain * deltaMinutes));
    }

    public static void RestoreNeed(SurvivalComponent survival, string needType, float amount)
    {
        switch (needType.ToLowerInvariant())
        {
            case "hunger":
                survival.Hunger = Math.Max(0f, survival.Hunger - amount);
                break;
            case "thirst":
                survival.Thirst = Math.Max(0f, survival.Thirst - amount);
                break;
            case "fatigue":
                survival.Fatigue = Math.Max(0f, survival.Fatigue - amount);
                break;
        }
    }

    private static void RemoveModifierBySource(StatsComponent stats, string source)
    {
        var mod = stats.Modifiers.FirstOrDefault(m => m.Source == source);
        if (mod != null) stats.RemoveModifier(mod);
    }

    private static void ApplyHungerEffects(SurvivalComponent survival, StatsComponent stats)
    {
        var threshold = SurvivalComponent.GetThreshold(survival.Hunger);

        RemoveModifierBySource(stats, HungerBuffSource);
        RemoveModifierBySource(stats, HungerDebuffSource);

        switch (threshold)
        {
            case SurvivalThreshold.Satisfied:
                stats.AddModifier(new StatModifier(Stat.Strength, 1, HungerBuffSource));
                break;
            case SurvivalThreshold.Deprived:
            case SurvivalThreshold.Critical:
                stats.AddModifier(new StatModifier(Stat.Strength, -1, HungerDebuffSource));
                break;
        }
    }

    private static void ApplyThirstEffects(SurvivalComponent survival, StatsComponent stats)
    {
        var threshold = SurvivalComponent.GetThreshold(survival.Thirst);

        RemoveModifierBySource(stats, ThirstBuffSource);
        RemoveModifierBySource(stats, ThirstDebuffSource);

        switch (threshold)
        {
            case SurvivalThreshold.Satisfied:
                stats.AddModifier(new StatModifier(Stat.Perception, 1, ThirstBuffSource));
                break;
            case SurvivalThreshold.Deprived:
            case SurvivalThreshold.Critical:
                stats.AddModifier(new StatModifier(Stat.Perception, -1, ThirstDebuffSource));
                break;
        }
    }

    private static void ApplyFatigueEffects(SurvivalComponent survival, StatsComponent stats)
    {
        var threshold = SurvivalComponent.GetThreshold(survival.Fatigue);

        RemoveModifierBySource(stats, FatigueBuffSource);
        RemoveModifierBySource(stats, FatigueDebuffSource);

        switch (threshold)
        {
            case SurvivalThreshold.Satisfied:
                stats.AddModifier(new StatModifier(Stat.Agility, 1, FatigueBuffSource));
                break;
            case SurvivalThreshold.Deprived:
            case SurvivalThreshold.Critical:
                stats.AddModifier(new StatModifier(Stat.Agility, -1, FatigueDebuffSource));
                break;
        }
    }
}

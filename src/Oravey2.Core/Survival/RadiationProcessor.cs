namespace Oravey2.Core.Survival;

using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Stats;

public sealed class RadiationProcessor
{
    public const int MildThreshold = 200;
    public const int SevereThreshold = 500;
    public const int CriticalThreshold = 800;
    public const int LethalThreshold = 1000;

    public const int NaturalDecayPerMinute = 1;
    public const int RadAwayReduction = 100;

    public const string RadMildSource = "Radiation_Mild";
    public const string RadSevereEndSource = "Radiation_Severe_End";
    public const string RadSevereStrSource = "Radiation_Severe_Str";
    public const string RadCritEndSource = "Radiation_Critical_End";
    public const string RadCritStrSource = "Radiation_Critical_Str";

    public const int CriticalHPDrain = 2;

    public void Evaluate(
        RadiationComponent radiation,
        StatsComponent stats,
        HealthComponent health,
        float deltaMinutes,
        bool inRadZone)
    {
        if (!inRadZone && radiation.Level > 0)
        {
            var decay = (int)(NaturalDecayPerMinute * deltaMinutes);
            radiation.Reduce(decay);
        }

        ClearRadModifiers(stats);

        if (radiation.Level >= LethalThreshold)
        {
            health.TakeDamage(health.CurrentHP);
            return;
        }

        if (radiation.Level >= CriticalThreshold)
        {
            stats.AddModifier(new StatModifier(Stat.Endurance, -3, RadCritEndSource));
            stats.AddModifier(new StatModifier(Stat.Strength, -2, RadCritStrSource));
            health.TakeDamage((int)(CriticalHPDrain * deltaMinutes));
        }
        else if (radiation.Level >= SevereThreshold)
        {
            stats.AddModifier(new StatModifier(Stat.Endurance, -2, RadSevereEndSource));
            stats.AddModifier(new StatModifier(Stat.Strength, -1, RadSevereStrSource));
        }
        else if (radiation.Level >= MildThreshold)
        {
            stats.AddModifier(new StatModifier(Stat.Endurance, -1, RadMildSource));
        }
    }

    public static void ApplyRadAway(RadiationComponent radiation)
    {
        radiation.Reduce(RadAwayReduction);
    }

    private static void ClearRadModifiers(StatsComponent stats)
    {
        foreach (var source in new[] { RadMildSource, RadSevereEndSource, RadSevereStrSource,
                                       RadCritEndSource, RadCritStrSource })
        {
            var mod = stats.Modifiers.FirstOrDefault(m => m.Source == source);
            if (mod != null) stats.RemoveModifier(mod);
        }
    }
}

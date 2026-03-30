namespace Oravey2.Core.Survival;

public sealed class SurvivalComponent
{
    public bool Enabled { get; set; } = true;

    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Fatigue { get; set; }

    public void Clamp()
    {
        Hunger = Math.Clamp(Hunger, 0f, 100f);
        Thirst = Math.Clamp(Thirst, 0f, 100f);
        Fatigue = Math.Clamp(Fatigue, 0f, 100f);
    }

    public static SurvivalThreshold GetThreshold(float value) => value switch
    {
        <= 25f => SurvivalThreshold.Satisfied,
        <= 50f => SurvivalThreshold.Normal,
        <= 75f => SurvivalThreshold.Deprived,
        _ => SurvivalThreshold.Critical
    };
}

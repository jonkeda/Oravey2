namespace Oravey2.Core.Character.Level;

public static class LevelFormulas
{
    public static int XPRequired(int level) => 100 * level * level;

    public static int SkillPointsPerLevel(int intelligence) => 5 + intelligence / 2;

    public static int MaxHP(int endurance, int level) => 50 + endurance * 10 + level * 5;

    public static float CarryWeight(int strength) => 50f + strength * 10f;
}

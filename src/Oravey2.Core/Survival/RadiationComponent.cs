namespace Oravey2.Core.Survival;

public sealed class RadiationComponent
{
    public int Level { get; set; }

    public void Expose(int amount)
    {
        Level = Math.Clamp(Level + amount, 0, 1000);
    }

    public void Reduce(int amount)
    {
        Level = Math.Clamp(Level - amount, 0, 1000);
    }
}

using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Character.Health;

public class HealthComponent
{
    private readonly StatsComponent _stats;
    private readonly LevelComponent _level;
    private readonly IEventBus? _eventBus;
    private readonly List<StatusEffect> _activeEffects = [];

    public int CurrentHP { get; private set; }
    public int MaxHP => LevelFormulas.MaxHP(_stats.GetEffective(Stat.Endurance), _level.Level);
    public int RadiationLevel { get; set; }
    public IReadOnlyList<StatusEffect> ActiveEffects => _activeEffects;
    public bool IsAlive => CurrentHP > 0;

    public HealthComponent(StatsComponent stats, LevelComponent level, IEventBus? eventBus = null)
    {
        _stats = stats;
        _level = level;
        _eventBus = eventBus;
        CurrentHP = MaxHP;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        var oldHP = CurrentHP;
        CurrentHP = Math.Max(0, CurrentHP - amount);
        _eventBus?.Publish(new HealthChangedEvent(oldHP, CurrentHP));
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        var oldHP = CurrentHP;
        CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        _eventBus?.Publish(new HealthChangedEvent(oldHP, CurrentHP));
    }

    public void HealToMax()
    {
        var oldHP = CurrentHP;
        CurrentHP = MaxHP;
        if (oldHP != CurrentHP)
            _eventBus?.Publish(new HealthChangedEvent(oldHP, CurrentHP));
    }

    internal void SetCurrent(int hp)
    {
        CurrentHP = Math.Clamp(hp, 0, MaxHP);
    }

    public void ApplyEffect(StatusEffect effect) => _activeEffects.Add(effect);

    public void RemoveEffect(StatusEffectType type)
        => _activeEffects.RemoveAll(e => e.Type == type);
}

using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Audio;

/// <summary>
/// Manages per-category audio volume levels with a master multiplier.
/// Pure logic — Stride audio service reads effective volumes and applies them.
/// </summary>
public sealed class VolumeSettings
{
    private readonly IEventBus _eventBus;
    private readonly Dictionary<AudioCategory, float> _volumes = new();

    public VolumeSettings(IEventBus eventBus)
    {
        _eventBus = eventBus;
        foreach (AudioCategory cat in Enum.GetValues<AudioCategory>())
            _volumes[cat] = 1f;
    }

    /// <summary>
    /// Gets the raw volume for a category (0–1).
    /// </summary>
    public float GetVolume(AudioCategory category) => _volumes[category];

    /// <summary>
    /// Sets the volume for a category (clamped 0–1). Publishes VolumeChangedEvent if changed.
    /// </summary>
    public void SetVolume(AudioCategory category, float volume)
    {
        float clamped = Math.Clamp(volume, 0f, 1f);
        float old = _volumes[category];
        if (Math.Abs(clamped - old) < 0.0001f) return;

        _volumes[category] = clamped;
        _eventBus.Publish(new VolumeChangedEvent(category, old, clamped));
    }

    /// <summary>
    /// Gets the effective volume for a category: category volume × master volume.
    /// For Master itself, returns the raw master value.
    /// </summary>
    public float GetEffectiveVolume(AudioCategory category)
    {
        if (category == AudioCategory.Master)
            return _volumes[AudioCategory.Master];

        return _volumes[category] * _volumes[AudioCategory.Master];
    }
}

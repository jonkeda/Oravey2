using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Settings;

/// <summary>
/// User-configurable game settings stored as string key-value pairs.
/// Publishes SettingChangedEvent on changes. Pure logic — persistence handled externally.
/// </summary>
public sealed class GameSettings
{
    // Well-known keys
    public const string KeyMasterVolume = "audio.master_volume";
    public const string KeyMusicVolume = "audio.music_volume";
    public const string KeySfxVolume = "audio.sfx_volume";
    public const string KeyAmbientVolume = "audio.ambient_volume";
    public const string KeyVoiceVolume = "audio.voice_volume";
    public const string KeyAutoSaveInterval = "save.autosave_interval";
    public const string KeyAutoSaveEnabled = "save.autosave_enabled";
    public const string KeyQualityPreset = "graphics.quality_preset";
    public const string KeyLanguage = "general.language";
    public const string KeyShowDamageNumbers = "ui.show_damage_numbers";
    public const string KeyShowMinimap = "ui.show_minimap";
    public const string KeyCameraZoomSensitivity = "camera.zoom_sensitivity";

    private static readonly Dictionary<string, string> Defaults = new()
    {
        [KeyMasterVolume] = "1.0",
        [KeyMusicVolume] = "1.0",
        [KeySfxVolume] = "1.0",
        [KeyAmbientVolume] = "1.0",
        [KeyVoiceVolume] = "1.0",
        [KeyAutoSaveInterval] = "300",
        [KeyAutoSaveEnabled] = "true",
        [KeyQualityPreset] = "High",
        [KeyLanguage] = "en",
        [KeyShowDamageNumbers] = "true",
        [KeyShowMinimap] = "true",
        [KeyCameraZoomSensitivity] = "1.0",
    };

    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, string> _values;

    public GameSettings(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _values = new Dictionary<string, string>(Defaults);
    }

    /// <summary>
    /// Gets a setting value. Returns the default if the key is known, or null if unknown.
    /// </summary>
    public string? Get(string key)
        => _values.TryGetValue(key, out var val) ? val : null;

    /// <summary>
    /// Gets a setting as float. Returns defaultValue if not parseable.
    /// </summary>
    public float GetFloat(string key, float defaultValue = 0f)
        => float.TryParse(Get(key), out var val) ? val : defaultValue;

    /// <summary>
    /// Gets a setting as bool. Returns defaultValue if not parseable.
    /// </summary>
    public bool GetBool(string key, bool defaultValue = false)
        => bool.TryParse(Get(key), out var val) ? val : defaultValue;

    /// <summary>
    /// Gets a setting as int. Returns defaultValue if not parseable.
    /// </summary>
    public int GetInt(string key, int defaultValue = 0)
        => int.TryParse(Get(key), out var val) ? val : defaultValue;

    /// <summary>
    /// Sets a value and publishes SettingChangedEvent if it differs from the current value.
    /// </summary>
    public void Set(string key, string value)
    {
        var old = Get(key);
        if (old == value) return;

        _values[key] = value;
        _eventBus.Publish(new SettingChangedEvent(key, old ?? "", value));
    }

    /// <summary>
    /// Resets a key to its default value. Publishes event if changed.
    /// </summary>
    public void ResetToDefault(string key)
    {
        if (Defaults.TryGetValue(key, out var def))
            Set(key, def);
    }

    /// <summary>
    /// Resets all settings to defaults. Publishes events for each change.
    /// </summary>
    public void ResetAllDefaults()
    {
        foreach (var (key, def) in Defaults)
            Set(key, def);
    }

    /// <summary>
    /// Returns all current settings as a readonly dictionary (for persistence).
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAll()
        => _values;

    /// <summary>
    /// Loads settings from a dictionary (e.g., from a file). Publishes events for changes.
    /// </summary>
    public void LoadFrom(IReadOnlyDictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
            Set(key, value);
    }

    /// <summary>All well-known default keys and values.</summary>
    public static IReadOnlyDictionary<string, string> DefaultValues => Defaults;
}

using Oravey2.Core.Framework.Events;
using Oravey2.Core.Settings;

namespace Oravey2.Tests.Settings;

public class GameSettingsTests
{
    private readonly EventBus _bus = new();
    private readonly GameSettings _settings;

    public GameSettingsTests()
    {
        _settings = new GameSettings(_bus);
    }

    [Fact]
    public void DefaultsLoadedOnConstruction()
    {
        Assert.Equal("1.0", _settings.Get(GameSettings.KeyMasterVolume));
        Assert.Equal("true", _settings.Get(GameSettings.KeyAutoSaveEnabled));
        Assert.Equal("High", _settings.Get(GameSettings.KeyQualityPreset));
        Assert.Equal("en", _settings.Get(GameSettings.KeyLanguage));
    }

    [Fact]
    public void GetReturnsValue()
    {
        _settings.Set(GameSettings.KeyLanguage, "fr");
        Assert.Equal("fr", _settings.Get(GameSettings.KeyLanguage));
    }

    [Fact]
    public void GetUnknownKeyReturnsNull()
    {
        Assert.Null(_settings.Get("nonexistent.key"));
    }

    [Fact]
    public void GetFloatParsesCorrectly()
    {
        _settings.Set(GameSettings.KeyMasterVolume, "0.75");
        Assert.Equal(0.75f, _settings.GetFloat(GameSettings.KeyMasterVolume));
    }

    [Fact]
    public void GetBoolParsesCorrectly()
    {
        Assert.True(_settings.GetBool(GameSettings.KeyAutoSaveEnabled));
        _settings.Set(GameSettings.KeyAutoSaveEnabled, "false");
        Assert.False(_settings.GetBool(GameSettings.KeyAutoSaveEnabled));
    }

    [Fact]
    public void GetIntParsesCorrectly()
    {
        Assert.Equal(300, _settings.GetInt(GameSettings.KeyAutoSaveInterval));
    }

    [Fact]
    public void SetPublishesSettingChangedEvent()
    {
        SettingChangedEvent? received = null;
        _bus.Subscribe<SettingChangedEvent>(e => received = e);

        _settings.Set(GameSettings.KeyLanguage, "de");

        Assert.NotNull(received);
        Assert.Equal(GameSettings.KeyLanguage, received.Value.Key);
        Assert.Equal("en", received.Value.OldValue);
        Assert.Equal("de", received.Value.NewValue);
    }

    [Fact]
    public void SetSameValueNoEvent()
    {
        SettingChangedEvent? received = null;
        _bus.Subscribe<SettingChangedEvent>(e => received = e);

        _settings.Set(GameSettings.KeyLanguage, "en"); // already "en"

        Assert.Null(received);
    }

    [Fact]
    public void ResetToDefaultRestoresValue()
    {
        _settings.Set(GameSettings.KeyLanguage, "jp");
        _settings.ResetToDefault(GameSettings.KeyLanguage);

        Assert.Equal("en", _settings.Get(GameSettings.KeyLanguage));
    }

    [Fact]
    public void ResetAllDefaultsRestoresAll()
    {
        _settings.Set(GameSettings.KeyLanguage, "jp");
        _settings.Set(GameSettings.KeyQualityPreset, "Low");

        _settings.ResetAllDefaults();

        Assert.Equal("en", _settings.Get(GameSettings.KeyLanguage));
        Assert.Equal("High", _settings.Get(GameSettings.KeyQualityPreset));
    }

    [Fact]
    public void LoadFromAppliesBulkSettings()
    {
        var bulk = new Dictionary<string, string>
        {
            { GameSettings.KeyLanguage, "es" },
            { GameSettings.KeyQualityPreset, "Medium" },
            { "custom.key", "custom_value" }
        };

        _settings.LoadFrom(bulk);

        Assert.Equal("es", _settings.Get(GameSettings.KeyLanguage));
        Assert.Equal("Medium", _settings.Get(GameSettings.KeyQualityPreset));
        Assert.Equal("custom_value", _settings.Get("custom.key"));
    }
}

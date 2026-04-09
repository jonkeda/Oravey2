using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Services;

public class MauiSettingsService : ISettingsService
{
    public string Get(string key, string defaultValue)
        => Preferences.Default.Get(key, defaultValue);

    public void Set(string key, string value)
        => Preferences.Default.Set(key, value);

    public Task<string?> GetSecureAsync(string key)
        => SecureStorage.Default.GetAsync(key);

    public async Task SetSecureAsync(string key, string value)
        => await SecureStorage.Default.SetAsync(key, value);
}

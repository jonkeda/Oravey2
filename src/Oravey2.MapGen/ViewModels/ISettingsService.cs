namespace Oravey2.MapGen.ViewModels;

public interface ISettingsService
{
    string Get(string key, string defaultValue);
    void Set(string key, string value);
    Task<string?> GetSecureAsync(string key);
    Task SetSecureAsync(string key, string value);
}

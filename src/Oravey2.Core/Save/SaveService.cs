using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.Save;

/// <summary>
/// Orchestrates save/load I/O. Uses SaveDataBuilder for capturing state
/// and SaveDataRestorer for restoring it. Handles file persistence.
/// </summary>
public sealed class SaveService
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _saveDirectory;
    private readonly string _savePath;

    public SaveService(string? saveDirectory = null)
    {
        _saveDirectory = saveDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Oravey2");
        _savePath = Path.Combine(_saveDirectory, "save.json");
    }

    /// <summary>
    /// Saves the given SaveData snapshot to disk.
    /// </summary>
    public void SaveGame(SaveData data)
    {
        Directory.CreateDirectory(_saveDirectory);
        var json = JsonSerializer.Serialize(data, _jsonOpts);
        File.WriteAllText(_savePath, json);
    }

    /// <summary>
    /// Loads a SaveData from disk and returns a restorer, or null if no save exists.
    /// </summary>
    public SaveDataRestorer? LoadGame()
    {
        if (!File.Exists(_savePath))
            return null;

        var json = File.ReadAllText(_savePath);
        var data = JsonSerializer.Deserialize<SaveData>(json, _jsonOpts);
        return data != null ? new SaveDataRestorer(data) : null;
    }

    /// <summary>
    /// Returns true if a save file exists on disk.
    /// </summary>
    public bool HasSaveFile() => File.Exists(_savePath);

    /// <summary>
    /// Deletes the save file if it exists.
    /// </summary>
    public void DeleteSave()
    {
        if (File.Exists(_savePath))
            File.Delete(_savePath);
    }

    /// <summary>
    /// Path where saves are stored. Exposed for automation/testing.
    /// </summary>
    public string SavePath => _savePath;
}

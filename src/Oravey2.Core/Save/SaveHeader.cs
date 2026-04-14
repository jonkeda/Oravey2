namespace Oravey2.Core.Save;

/// <summary>
/// Metadata for a save file. Displayed on the load screen without deserializing the full SaveData.
/// </summary>
public sealed class SaveHeader
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; }
    public string GameVersion { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string PlayerName { get; set; } = "";
    public int PlayerLevel { get; set; }
    public TimeSpan PlayTime { get; set; }
}

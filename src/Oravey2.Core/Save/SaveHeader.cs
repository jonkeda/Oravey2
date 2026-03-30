namespace Oravey2.Core.Save;

/// <summary>
/// Metadata for a save file. Displayed on the load screen without deserializing the full SaveData.
/// </summary>
public sealed record SaveHeader(
    int FormatVersion,
    string GameVersion,
    DateTime Timestamp,
    string PlayerName,
    int PlayerLevel,
    TimeSpan PlayTime
)
{
    public const int CurrentFormatVersion = 1;
}

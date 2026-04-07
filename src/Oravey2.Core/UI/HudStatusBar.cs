namespace Oravey2.Core.UI;

/// <summary>
/// Pure-logic HUD status bar model. Computes display state from game data.
/// No Stride dependencies.
/// </summary>
public sealed class HudStatusBar
{
    /// <summary>Current region / location name.</summary>
    public string LocationName { get; set; } = "Unknown";

    /// <summary>Player tile X coordinate.</summary>
    public int TileX { get; set; }

    /// <summary>Player tile Y coordinate.</summary>
    public int TileY { get; set; }

    /// <summary>In-game hour (0–23.99…).</summary>
    public float GameHour { get; set; }

    /// <summary>Day count since game start.</summary>
    public int DayCount { get; set; } = 1;

    /// <summary>Current weather icon identifier.</summary>
    public string WeatherIcon { get; set; } = "clear";

    /// <summary>Temperature in °C.</summary>
    public int Temperature { get; set; } = 20;

    /// <summary>Seconds since the last data change.</summary>
    public float TimeSinceChange { get; private set; }

    /// <summary>Auto-hide threshold in seconds.</summary>
    public const float AutoHideDelay = 5f;

    /// <summary>Whether the bar should currently be visible.</summary>
    public bool IsVisible => TimeSinceChange < AutoHideDelay;

    /// <summary>
    /// Updates the bar with new data. Resets the auto-hide timer if any value changed.
    /// </summary>
    public void Update(string locationName, int tileX, int tileY,
                       float gameHour, int dayCount,
                       string weatherIcon, int temperature,
                       float deltaTime)
    {
        bool changed = locationName != LocationName
                    || tileX != TileX
                    || tileY != TileY
                    || MathF.Abs(gameHour - GameHour) > 0.01f
                    || dayCount != DayCount
                    || weatherIcon != WeatherIcon
                    || temperature != Temperature;

        LocationName = locationName;
        TileX = tileX;
        TileY = tileY;
        GameHour = gameHour;
        DayCount = dayCount;
        WeatherIcon = weatherIcon;
        Temperature = temperature;

        if (changed)
            TimeSinceChange = 0f;
        else
            TimeSinceChange += deltaTime;
    }

    /// <summary>Formatted time string (HH:MM).</summary>
    public string FormattedTime
    {
        get
        {
            int hours = (int)GameHour;
            int minutes = (int)((GameHour - hours) * 60);
            return $"{hours:D2}:{minutes:D2}";
        }
    }

    /// <summary>Formatted coordinate string.</summary>
    public string FormattedCoordinates => $"({TileX}, {TileY})";
}

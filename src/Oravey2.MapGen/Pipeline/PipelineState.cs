namespace Oravey2.MapGen.Pipeline;

public sealed class PipelineState
{
    public string RegionName { get; set; } = string.Empty;
    public string ContentPackPath { get; set; } = string.Empty;
    public int CurrentStep { get; set; } = 1;

    public RegionStepState Region { get; set; } = new();
    public DownloadStepState Download { get; set; } = new();
    public ParseStepState Parse { get; set; } = new();
    public TownSelectionStepState TownSelection { get; set; } = new();
    public TownDesignStepState TownDesign { get; set; } = new();
    public TownMapsStepState TownMaps { get; set; } = new();
    public AssetsStepState Assets { get; set; } = new();
    public AssemblyStepState Assembly { get; set; } = new();

    /// <summary>
    /// Returns true if the given step number (1-based) is completed.
    /// </summary>
    public bool IsStepCompleted(int step) => step switch
    {
        1 => Region.Completed,
        2 => Download.Completed,
        3 => Parse.Completed,
        4 => TownSelection.Completed,
        5 => TownDesign.Completed,
        6 => TownMaps.Completed,
        7 => Assets.Completed,
        8 => Assembly.Completed,
        _ => false,
    };

    /// <summary>
    /// Returns true if the given step number (1-based) is unlocked.
    /// Step 1 is always unlocked. Others require the previous step completed.
    /// </summary>
    public bool IsStepUnlocked(int step)
    {
        if (step <= 1) return true;
        return IsStepCompleted(step - 1);
    }

    /// <summary>
    /// Advances CurrentStep to the next step if the current step is completed.
    /// Returns true if advancement occurred.
    /// </summary>
    public bool TryAdvance()
    {
        if (CurrentStep >= 8) return false;
        if (!IsStepCompleted(CurrentStep)) return false;
        CurrentStep++;
        return true;
    }
}

public sealed class RegionStepState
{
    public bool Completed { get; set; }
    public string? PresetName { get; set; }
    public double NorthLat { get; set; }
    public double SouthLat { get; set; }
    public double EastLon { get; set; }
    public double WestLon { get; set; }
    public string? OsmDownloadUrl { get; set; }
}

public sealed class DownloadStepState
{
    public bool Completed { get; set; }
    public bool SrtmDownloaded { get; set; }
    public bool OsmDownloaded { get; set; }
}

public sealed class ParseStepState
{
    public bool Completed { get; set; }
    public bool TemplateSaved { get; set; }
    public int TownCount { get; set; }
    public int RoadCount { get; set; }
    public int WaterBodyCount { get; set; }
    public int SrtmTileCount { get; set; }
    public int FilteredTownCount { get; set; }
    public int FilteredRoadCount { get; set; }
    public int FilteredWaterBodyCount { get; set; }
}

public sealed class TownSelectionStepState
{
    public bool Completed { get; set; }
    public string? Mode { get; set; }
    public int TownCount { get; set; }
}

public sealed class TownDesignStepState
{
    public bool Completed { get; set; }
    public List<string> Designed { get; set; } = [];
    public int Remaining { get; set; }
}

public sealed class TownMapsStepState
{
    public bool Completed { get; set; }
    public List<string> Generated { get; set; } = [];
    public int Remaining { get; set; }
}

public sealed class AssetsStepState
{
    public bool Completed { get; set; }
    public int Ready { get; set; }
    public int Pending { get; set; }
    public int Failed { get; set; }
}

public sealed class AssemblyStepState
{
    public bool Completed { get; set; }
    public bool Validated { get; set; }
}

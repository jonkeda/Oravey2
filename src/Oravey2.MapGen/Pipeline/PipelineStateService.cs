using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Pipeline;

public sealed class PipelineStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _dataRoot;

    public string DataRoot => _dataRoot;

    public PipelineStateService(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    /// <summary>
    /// Loads the pipeline state for the given region.
    /// Returns a default state if the file does not exist.
    /// </summary>
    public async Task<PipelineState> LoadAsync(string regionName, CancellationToken ct = default)
    {
        var path = GetStatePath(regionName);

        if (!File.Exists(path))
        {
            return new PipelineState { RegionName = regionName };
        }

        await using var stream = File.OpenRead(path);
        var state = await JsonSerializer.DeserializeAsync<PipelineState>(stream, JsonOptions, ct);
        return state ?? new PipelineState { RegionName = regionName };
    }

    /// <summary>
    /// Saves the pipeline state for the region specified in the state object.
    /// Creates intermediate directories if they don't exist.
    /// </summary>
    public async Task SaveAsync(PipelineState state, CancellationToken ct = default)
    {
        var path = GetStatePath(state.RegionName);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, ct);
    }

    internal string GetStatePath(string regionName)
    {
        return Path.Combine(_dataRoot, "regions", regionName, "pipeline-state.json");
    }
}

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public record ContentPackInfo(
    string DirectoryName,
    string Id,
    string Name,
    string? RegionCode,
    string Description,
    string? Parent);

public record ContentPackSpec(string Name, string? RegionCode, string? Parent);

public class RegionStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    private string _contentRoot = string.Empty;
    public string ContentRoot => _contentRoot;

    public ObservableCollection<ContentPackInfo> ContentPackInfos { get; } = [];

    private ContentPackInfo? _selectedPackInfo;
    public ContentPackInfo? SelectedPackInfo
    {
        get => _selectedPackInfo;
        set
        {
            if (SetProperty(ref _selectedPackInfo, value))
            {
                OnPropertyChanged(nameof(HasSelectedPack));
                OnPropertyChanged(nameof(SelectedPackHierarchy));
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPack => SelectedPackInfo is not null;

    public string SelectedPackHierarchy
    {
        get
        {
            if (SelectedPackInfo is null) return string.Empty;
            return BuildHierarchyText(SelectedPackInfo);
        }
    }

    public bool CanComplete => HasSelectedPack;

    private readonly RelayCommand _nextCommand;
    public ICommand NextCommand => _nextCommand;

    private readonly RelayCommand _createContentPackCommand;
    public ICommand CreateContentPackCommand => _createContentPackCommand;

    public Action? StepCompleted { get; set; }
    public Action? CreateContentPackRequested { get; set; }

    public RegionStepViewModel()
    {
        _nextCommand = new RelayCommand(OnNext, () => CanComplete);
        _createContentPackCommand = new RelayCommand(
            () => CreateContentPackRequested?.Invoke());
    }

    public void Initialize(string? contentRoot = null)
    {
        if (contentRoot is not null)
            ScanContentPacks(contentRoot);
    }

    public void Load(PipelineState state)
    {
        _state = state;

        if (!string.IsNullOrEmpty(state.ContentPackPath))
        {
            var dirName = Path.GetFileName(state.ContentPackPath);
            SelectedPackInfo = ContentPackInfos.FirstOrDefault(
                p => string.Equals(p.DirectoryName, dirName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void ScanContentPacks(string contentRoot)
    {
        ContentPackInfos.Clear();
        _contentRoot = contentRoot;
        if (!Directory.Exists(contentRoot)) return;

        foreach (var dir in Directory.GetDirectories(contentRoot).Order())
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            var dto = ReadManifest(manifestPath);
            if (dto is null) continue;

            // Only show packs with a regionCode (leaf packs that the pipeline operates on)
            if (string.IsNullOrEmpty(dto.RegionCode)) continue;

            ContentPackInfos.Add(new ContentPackInfo(
                DirectoryName: Path.GetFileName(dir),
                Id: dto.Id,
                Name: dto.Name,
                RegionCode: dto.RegionCode,
                Description: dto.Description,
                Parent: dto.Parent));
        }
    }

    public List<string> GetExistingGenres()
    {
        if (string.IsNullOrEmpty(_contentRoot) || !Directory.Exists(_contentRoot))
            return [];

        var genres = new List<string>();
        foreach (var dir in Directory.GetDirectories(_contentRoot).Order())
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            var dto = ReadManifest(manifestPath);
            if (dto is null) continue;

            // Genre root packs have no parent and no regionCode
            if (string.IsNullOrEmpty(dto.Parent) && string.IsNullOrEmpty(dto.RegionCode))
            {
                // Extract genre from directory name: "Oravey2.Apocalyptic" → "Apocalyptic"
                var dirName = Path.GetFileName(dir);
                var parts = dirName.Split('.');
                if (parts.Length >= 2)
                    genres.Add(parts[1]);
            }
        }
        return genres;
    }

    public static List<ContentPackSpec> BuildPackChain(string genre, GeofabrikRegion region)
    {
        var regionCode = region.Iso3166_2?.FirstOrDefault()
                      ?? region.Iso3166Alpha2?.FirstOrDefault()
                      ?? throw new ArgumentException("Region has no ISO code");

        var chain = new List<ContentPackSpec>();

        // Genre root
        chain.Add(new ContentPackSpec(
            Name: $"Oravey2.{genre}",
            RegionCode: null,
            Parent: null));

        // Country (first segment before '-')
        var country = regionCode.Split('-')[0];
        chain.Add(new ContentPackSpec(
            Name: $"Oravey2.{genre}.{country}",
            RegionCode: country,
            Parent: $"oravey2.{genre.ToLowerInvariant()}"));

        // Region (if code has a '-')
        if (regionCode.Contains('-'))
        {
            var sub = regionCode.Split('-')[1];
            chain.Add(new ContentPackSpec(
                Name: $"Oravey2.{genre}.{country}.{sub}",
                RegionCode: regionCode,
                Parent: $"oravey2.{genre.ToLowerInvariant()}.{country.ToLowerInvariant()}"));
        }

        return chain;
    }

    public void EnsurePackChain(string genre, GeofabrikRegion region)
    {
        if (string.IsNullOrEmpty(_contentRoot)) return;

        var chain = BuildPackChain(genre, region);

        foreach (var spec in chain)
        {
            var packDir = Path.Combine(_contentRoot, spec.Name);
            if (Directory.Exists(packDir)) continue;

            Directory.CreateDirectory(Path.Combine(packDir, "data"));
            Directory.CreateDirectory(Path.Combine(packDir, "towns"));
            Directory.CreateDirectory(Path.Combine(packDir, "overworld"));
            Directory.CreateDirectory(Path.Combine(packDir, "assets", "meshes"));

            var displayName = spec.RegionCode is not null
                ? region.Name
                : genre;

            var manifest = new ManifestDto(
                Id: spec.Name.ToLowerInvariant(),
                Name: displayName,
                Version: "0.1.0",
                Description: spec.RegionCode is not null
                    ? $"Content pack for {displayName}"
                    : $"{genre} world-shared content",
                Author: "",
                Parent: spec.Parent ?? "",
                RegionCode: spec.RegionCode);

            File.WriteAllText(
                Path.Combine(packDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, ContentPackSerializer.WriteOptions));
        }

        // Ensure region preset exists
        EnsureRegionPreset(region);

        // Refresh and select the leaf pack
        ScanContentPacks(_contentRoot);
        var leaf = chain.Last();
        SelectedPackInfo = ContentPackInfos.FirstOrDefault(
            p => string.Equals(p.DirectoryName, leaf.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureRegionPreset(GeofabrikRegion region)
    {
        var presetsDir = Path.Combine("data", "presets");
        Directory.CreateDirectory(presetsDir);

        var preset = region.ToRegionPreset();
        var presetPath = Path.Combine(presetsDir, $"{preset.Name}.regionpreset");
        if (!File.Exists(presetPath))
            preset.Save(presetPath);
    }

    private string BuildHierarchyText(ContentPackInfo pack)
    {
        var chain = new List<string> { pack.DirectoryName };
        var parentId = pack.Parent;

        while (!string.IsNullOrEmpty(parentId))
        {
            var parentPack = ContentPackInfos.FirstOrDefault(
                p => string.Equals(p.Id, parentId, StringComparison.OrdinalIgnoreCase));

            if (parentPack is not null)
            {
                chain.Add(parentPack.DirectoryName);
                parentId = parentPack.Parent;
            }
            else
            {
                // Look in all packs (including non-leaf genre roots)
                var found = FindPackById(parentId);
                if (found is not null)
                {
                    chain.Add(found);
                    break; // genre root has no parent
                }
                break;
            }
        }

        chain.Reverse();
        return string.Join(" → ", chain);
    }

    private string? FindPackById(string id)
    {
        if (string.IsNullOrEmpty(_contentRoot)) return null;

        foreach (var dir in Directory.GetDirectories(_contentRoot))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;
            var dto = ReadManifest(manifestPath);
            if (dto is not null && string.Equals(dto.Id, id, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(dir);
        }
        return null;
    }

    public RegionPreset? FindPresetByRegionCode(string regionCode)
    {
        var presetsDir = Path.Combine("data", "presets");
        if (!Directory.Exists(presetsDir)) return null;

        foreach (var file in Directory.GetFiles(presetsDir, "*.regionpreset"))
        {
            try
            {
                var preset = RegionPreset.Load(file);
                if (string.Equals(preset.RegionCode, regionCode, StringComparison.OrdinalIgnoreCase))
                    return preset;
            }
            catch { /* skip invalid presets */ }
        }
        return null;
    }

    private static ManifestDto? ReadManifest(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ManifestDto>(json, ContentPackSerializer.ReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private void OnNext()
    {
        if (SelectedPackInfo is null) return;

        _state.ContentPackPath = Path.Combine(_contentRoot, SelectedPackInfo.DirectoryName);
        _state.Region.Completed = true;

        // Resolve region from pack's regionCode
        if (!string.IsNullOrEmpty(SelectedPackInfo.RegionCode))
        {
            var preset = FindPresetByRegionCode(SelectedPackInfo.RegionCode);
            if (preset is not null)
            {
                _state.RegionName = preset.Name;
                _state.Region.PresetName = preset.Name;
                _state.Region.NorthLat = preset.NorthLat;
                _state.Region.SouthLat = preset.SouthLat;
                _state.Region.EastLon = preset.EastLon;
                _state.Region.WestLon = preset.WestLon;
                _state.Region.OsmDownloadUrl = preset.OsmDownloadUrl;
            }
        }

        StepCompleted?.Invoke();
    }
}

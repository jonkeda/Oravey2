using Oravey2.MapGen.RegionTemplates;

const double DefaultOriginLat = 52.50;
const double DefaultOriginLon = 4.95;
const double DefaultGridCellSize = 30.0; // ~30 m SRTM resolution

string? srtmDir = null;
string? osmFile = null;
string? outputFile = null;
string? regionArg = null;
string? nameArg = null;
string? cullFile = null;

// Parse command-line arguments
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--srtm" when i + 1 < args.Length:
            srtmDir = args[++i];
            break;
        case "--osm" when i + 1 < args.Length:
            osmFile = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputFile = args[++i];
            break;
        case "--region" when i + 1 < args.Length:
            regionArg = args[++i];
            break;
        case "--name" when i + 1 < args.Length:
            nameArg = args[++i];
            break;
        case "--cull" when i + 1 < args.Length:
            cullFile = args[++i];
            break;
        case "--help":
        case "-h":
            Console.WriteLine("Usage: Oravey2.RegionTemplateTool --region <name> [overrides]");
            Console.WriteLine("   or: Oravey2.RegionTemplateTool --srtm <dir> --osm <file> --output <file> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --region <name>    Region name (resolves paths from data/regions/<name>/).");
            Console.WriteLine("  --srtm <dir>       Directory containing .hgt/.hgt.gz SRTM files. Overrides --region.");
            Console.WriteLine("  --osm <file>       Path to an OSM PBF extract file. Overrides --region.");
            Console.WriteLine("  --output <file>    Output .RegionTemplateFile file path. Overrides --region.");
            Console.WriteLine("  --name <region>    Region name for the template (default: from --region or NoordHolland).");
            Console.WriteLine("  --cull <file>      Apply culling from a .cullsettings JSON file.");
            Console.WriteLine("                     When --region is used and --cull is omitted, preset cull settings apply.");
            Console.WriteLine("  --help, -h         Show this help message.");
            return 0;
    }
}

// Resolve paths from region preset if --region specified
RegionPreset? preset = null;
if (regionArg != null)
{
    var presetPath = Path.Combine("data", "regions", regionArg, "region.json");
    if (!File.Exists(presetPath))
    {
        Console.Error.WriteLine($"Region preset not found: {presetPath}");
        return 1;
    }
    preset = RegionPreset.Load(presetPath);

    srtmDir ??= preset.SrtmDir;
    osmFile ??= preset.OsmFilePath;
    outputFile ??= preset.OutputFilePath;
}

if (srtmDir == null || osmFile == null || outputFile == null)
{
    Console.Error.WriteLine(
        "Usage: Oravey2.RegionTemplateTool --region <name> [overrides]\n" +
        "   or: Oravey2.RegionTemplateTool --srtm <dir> --osm <file> --output <file> [options]");
    return 1;
}

if (!Directory.Exists(srtmDir))
{
    Console.Error.WriteLine($"SRTM directory not found: {srtmDir}");
    return 1;
}

if (!File.Exists(osmFile))
{
    Console.Error.WriteLine($"OSM PBF file not found: {osmFile}");
    return 1;
}

// Load cull settings if provided
CullSettings? cullSettings = null;
if (cullFile != null)
{
    if (!File.Exists(cullFile))
    {
        Console.Error.WriteLine($"Cull settings file not found: {cullFile}");
        return 1;
    }

    try
    {
        cullSettings = CullSettings.Load(cullFile);
        Console.WriteLine($"Loaded cull settings from: {cullFile}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to parse cull settings: {ex.Message}");
        return 1;
    }
}

// Use preset cull settings as default when --cull not specified
cullSettings ??= preset?.DefaultCullSettings;

var regionName = nameArg ?? preset?.Name ?? "NoordHolland";

Console.WriteLine($"RegionTemplateFile Builder — {regionName}");
Console.WriteLine($"  SRTM dir: {srtmDir}");
Console.WriteLine($"  OSM PBF:  {osmFile}");
Console.WriteLine($"  Output:   {outputFile}");

var geoMapper = new GeoMapper(DefaultOriginLat, DefaultOriginLon);

// Parse SRTM elevation
Console.Write("Parsing SRTM elevation data...");
var srtmParser = new SrtmParser(geoMapper);
float[,]? combinedGrid = null;
var hgtFiles = Directory.GetFiles(srtmDir)
    .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
              || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
    .ToArray();

if (hgtFiles.Length == 0)
{
    Console.Error.WriteLine($"\nNo .hgt/.hgt.gz files found in {srtmDir}");
    return 1;
}

foreach (var hgtFile in hgtFiles)
{
    Console.Write($"\n  Reading {Path.GetFileName(hgtFile)}...");
    var grid = srtmParser.ParseHgtFile(hgtFile);
    combinedGrid ??= grid;
    // For multi-tile support, grids would be stitched here.
    // For Noord-Holland starter, a single tile is expected.
}
Console.WriteLine(" Done.");

// Parse OSM features
Console.Write("Parsing OSM PBF data...");
var osmParser = new OsmParser(geoMapper);
var osmData = osmParser.ParsePbf(osmFile);
Console.WriteLine(" Done.");

Console.WriteLine($"  Towns:      {osmData.Towns.Count}");
Console.WriteLine($"  Roads:      {osmData.Roads.Count}");
Console.WriteLine($"  Water:      {osmData.WaterBodies.Count}");
Console.WriteLine($"  Railways:   {osmData.Railways.Count}");
Console.WriteLine($"  Land use:   {osmData.LandUseZones.Count}");

// Apply culling if settings provided
var towns = osmData.Towns;
var roads = osmData.Roads;
var water = osmData.WaterBodies;

if (cullSettings != null)
{
    int origTowns = towns.Count, origRoads = roads.Count, origWater = water.Count;

    towns = FeatureCuller.CullTowns(towns, cullSettings);
    roads = FeatureCuller.CullRoads(roads, towns, cullSettings);
    water = FeatureCuller.CullWater(water, cullSettings);

    Console.WriteLine($"Culling: {origTowns} towns → {towns.Count}, " +
                      $"{origRoads} roads → {roads.Count}, " +
                      $"{origWater} water → {water.Count}");

    osmData = new OsmExtract(towns, roads, water, osmData.Railways, osmData.LandUseZones);
}

// Build RegionTemplateFile
Console.Write("Building RegionTemplateFile...");
var builder = new RegionTemplateBuilder(geoMapper);
var template = builder.Build(regionName, combinedGrid!, osmData,
    DefaultOriginLat, DefaultOriginLon, DefaultGridCellSize);
Console.WriteLine(" Done.");

// Serialize
Console.Write($"Writing {outputFile}...");
var outputDir = Path.GetDirectoryName(outputFile);
if (!string.IsNullOrEmpty(outputDir))
    Directory.CreateDirectory(outputDir);

RegionTemplateBuilder.Serialize(template, outputFile);
var fileInfo = new FileInfo(outputFile);
Console.WriteLine($" Done. ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)");

Console.WriteLine($"\nExtracted {towns.Count} towns, {roads.Count} road segments, {water.Count} water features. Output: {outputFile} ({fileInfo.Length / (1024.0 * 1024.0):F1} MB).");
return 0;

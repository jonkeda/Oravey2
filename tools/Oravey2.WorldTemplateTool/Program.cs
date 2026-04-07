using Oravey2.MapGen.WorldTemplate;

const double DefaultOriginLat = 52.50;
const double DefaultOriginLon = 4.95;
const double DefaultGridCellSize = 30.0; // ~30 m SRTM resolution

string? srtmDir = null;
string? osmFile = null;
string? outputFile = null;
string regionName = "NoordHolland";

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
        case "--name" when i + 1 < args.Length:
            regionName = args[++i];
            break;
    }
}

if (srtmDir == null || osmFile == null || outputFile == null)
{
    Console.Error.WriteLine("Usage: Oravey2.WorldTemplateTool --srtm <dir> --osm <file.osm.pbf> --output <file.worldtemplate> [--name <region>]");
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

Console.WriteLine($"WorldTemplate Builder — {regionName}");
Console.WriteLine($"  SRTM dir: {srtmDir}");
Console.WriteLine($"  OSM PBF:  {osmFile}");
Console.WriteLine($"  Output:   {outputFile}");

var geoMapper = new GeoMapper(DefaultOriginLat, DefaultOriginLon);

// Parse SRTM elevation
Console.Write("Parsing SRTM elevation data...");
var srtmParser = new SrtmParser(geoMapper);
float[,]? combinedGrid = null;
var hgtFiles = Directory.GetFiles(srtmDir, "*.hgt");

if (hgtFiles.Length == 0)
{
    Console.Error.WriteLine($"\nNo .hgt files found in {srtmDir}");
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

// Build WorldTemplate
Console.Write("Building WorldTemplate...");
var builder = new WorldTemplateBuilder(geoMapper);
var template = builder.Build(regionName, combinedGrid!, osmData,
    DefaultOriginLat, DefaultOriginLon, DefaultGridCellSize);
Console.WriteLine(" Done.");

// Serialize
Console.Write($"Writing {outputFile}...");
var outputDir = Path.GetDirectoryName(outputFile);
if (!string.IsNullOrEmpty(outputDir))
    Directory.CreateDirectory(outputDir);

WorldTemplateBuilder.Serialize(template, outputFile);
var fileInfo = new FileInfo(outputFile);
Console.WriteLine($" Done. ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)");

Console.WriteLine($"\nExtracted {osmData.Towns.Count} towns, {osmData.Roads.Count} road segments, {osmData.WaterBodies.Count} water features. Output: {outputFile} ({fileInfo.Length / (1024.0 * 1024.0):F1} MB).");
return 0;

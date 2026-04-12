using Oravey2.Core.Data;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <content-pack-dir>");
    return 1;
}

var packDir = Path.GetFullPath(args[0]);
var dbPath = Path.Combine(packDir, "world.db");

Console.WriteLine($"Pack: {packDir}");
Console.WriteLine($"DB:   {dbPath}");

if (File.Exists(dbPath))
{
    File.Delete(dbPath);
    Console.WriteLine("Deleted existing world.db");
}

using var store = new WorldMapStore(dbPath);
var importer = new ContentPackImporter(store);
var result = importer.Import(packDir);

Console.WriteLine($"Towns={result.TownsImported} Chunks={result.ChunksWritten} Entities={result.EntitySpawnsInserted} LinFeat={result.LinearFeaturesInserted} POIs={result.PoisInserted}");
foreach (var w in result.Warnings)
    Console.WriteLine($"WARN: {w}");

return 0;

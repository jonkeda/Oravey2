using Oravey2.Core.Data;

namespace Oravey2.MapGen.Pipeline;

public sealed class ContentPackExporter
{
    public ImportResult Export(string contentPackPath, string dbPath)
    {
        using var store = new WorldMapStore(dbPath);
        var importer = new ContentPackImporter(store);
        return importer.Import(contentPackPath);
    }
}

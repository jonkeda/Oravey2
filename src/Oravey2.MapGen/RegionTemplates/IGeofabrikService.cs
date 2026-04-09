namespace Oravey2.MapGen.RegionTemplates;

public interface IGeofabrikService
{
    /// <summary>
    /// Fetch or load cached Geofabrik index. Returns the tree-structured index.
    /// </summary>
    Task<GeofabrikIndex> GetIndexAsync(bool forceRefresh = false);
}

using System.Text.Json;

namespace Oravey2.MapGen.WorldTemplate;

/// <summary>
/// Parses and queries the Geofabrik index-v1.json.
/// </summary>
public class GeofabrikIndex
{
    public IReadOnlyList<GeofabrikRegion> Roots { get; }
    public IReadOnlyDictionary<string, GeofabrikRegion> ById { get; }

    private GeofabrikIndex(IReadOnlyList<GeofabrikRegion> roots, IReadOnlyDictionary<string, GeofabrikRegion> byId)
    {
        Roots = roots;
        ById = byId;
    }

    public static GeofabrikIndex Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var features = doc.RootElement.GetProperty("features");

        var byId = new Dictionary<string, GeofabrikRegion>();

        // First pass: create all regions
        foreach (var feature in features.EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var id = props.GetProperty("id").GetString()!;
            string? parent = props.TryGetProperty("parent", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : null;
            var name = props.TryGetProperty("name", out var n) ? n.GetString()! : id;

            string? pbfUrl = null;
            if (props.TryGetProperty("urls", out var urls) &&
                urls.TryGetProperty("pbf", out var pbf))
            {
                pbfUrl = pbf.GetString();
            }

            string[]? iso2 = null;
            if (props.TryGetProperty("iso3166-1:alpha2", out var isoElem) && isoElem.ValueKind == JsonValueKind.Array)
                iso2 = isoElem.EnumerateArray().Select(e => e.GetString()!).ToArray();

            string[]? iso3166_2 = null;
            if (props.TryGetProperty("iso3166-2", out var iso2Elem) && iso2Elem.ValueKind == JsonValueKind.Array)
                iso3166_2 = iso2Elem.EnumerateArray().Select(e => e.GetString()!).ToArray();

            BoundingBox? bbox = null;
            if (feature.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object)
            {
                bbox = ExtractBounds(geometry);
            }

            byId[id] = new GeofabrikRegion
            {
                Id = id,
                Parent = parent,
                Name = name,
                PbfUrl = pbfUrl,
                Iso3166Alpha2 = iso2,
                Iso3166_2 = iso3166_2,
                Bounds = bbox
            };
        }

        // Second pass: build tree
        var roots = new List<GeofabrikRegion>();
        foreach (var region in byId.Values)
        {
            if (region.Parent is not null && byId.TryGetValue(region.Parent, out var parentRegion))
                parentRegion.Children.Add(region);
            else
                roots.Add(region);
        }

        // Sort children by name
        foreach (var region in byId.Values)
            region.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        roots.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return new GeofabrikIndex(roots, byId);
    }

    public IEnumerable<GeofabrikRegion> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) yield break;

        var q = query.Trim();
        foreach (var region in ById.Values)
        {
            if (region.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                yield return region;
                continue;
            }

            if (region.Iso3166Alpha2?.Any(c => c.Contains(q, StringComparison.OrdinalIgnoreCase)) == true)
            {
                yield return region;
                continue;
            }

            if (region.Iso3166_2?.Any(c => c.Contains(q, StringComparison.OrdinalIgnoreCase)) == true)
                yield return region;
        }
    }

    private static BoundingBox? ExtractBounds(JsonElement geometry)
    {
        var type = geometry.GetProperty("type").GetString();
        if (type is not "Polygon" and not "MultiPolygon")
            return null;

        double minLat = double.MaxValue, maxLat = double.MinValue;
        double minLon = double.MaxValue, maxLon = double.MinValue;

        var coords = geometry.GetProperty("coordinates");
        // MultiPolygon: [[[ring]]], Polygon: [[ring]]
        if (type == "MultiPolygon")
        {
            foreach (var polygon in coords.EnumerateArray())
                foreach (var ring in polygon.EnumerateArray())
                    AccumulateRing(ring, ref minLat, ref maxLat, ref minLon, ref maxLon);
        }
        else // Polygon
        {
            foreach (var ring in coords.EnumerateArray())
                AccumulateRing(ring, ref minLat, ref maxLat, ref minLon, ref maxLon);
        }

        if (minLat > maxLat) return null;
        return new BoundingBox(maxLat, minLat, maxLon, minLon);
    }

    private static void AccumulateRing(JsonElement ring,
        ref double minLat, ref double maxLat, ref double minLon, ref double maxLon)
    {
        foreach (var point in ring.EnumerateArray())
        {
            var lon = point[0].GetDouble();
            var lat = point[1].GetDouble();
            if (lat < minLat) minLat = lat;
            if (lat > maxLat) maxLat = lat;
            if (lon < minLon) minLon = lon;
            if (lon > maxLon) maxLon = lon;
        }
    }
}

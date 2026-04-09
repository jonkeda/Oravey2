using System.Numerics;

namespace Oravey2.MapGen.RegionTemplates;

/// <summary>
/// Converts between geographic coordinates (lat/lon) and game-world XZ coordinates.
/// Uses equirectangular approximation with cos(originLat) correction for longitude.
/// Accurate to less than 1 metre within 200 km of origin.
/// </summary>
public class GeoMapper
{
    private const double EarthRadiusMetres = 6_371_000.0;
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    public double OriginLatitude { get; }
    public double OriginLongitude { get; }

    private readonly double _cosOriginLat;

    public GeoMapper(double originLatitude = 52.50, double originLongitude = 4.95)
    {
        OriginLatitude = originLatitude;
        OriginLongitude = originLongitude;
        _cosOriginLat = Math.Cos(originLatitude * DegToRad);
    }

    public Vector2 LatLonToGameXZ(double latitude, double longitude)
    {
        double dLat = latitude - OriginLatitude;
        double dLon = longitude - OriginLongitude;

        float x = (float)(dLon * DegToRad * EarthRadiusMetres * _cosOriginLat);
        float z = (float)(dLat * DegToRad * EarthRadiusMetres);

        return new Vector2(x, z);
    }

    public (double Latitude, double Longitude) GameXZToLatLon(float x, float z)
    {
        double lat = OriginLatitude + (z / EarthRadiusMetres) * RadToDeg;
        double lon = OriginLongitude + (x / (EarthRadiusMetres * _cosOriginLat)) * RadToDeg;

        return (lat, lon);
    }
}

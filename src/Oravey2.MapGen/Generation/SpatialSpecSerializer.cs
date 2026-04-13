using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Serializes and deserializes TownSpatialSpecification to/from JSON.
/// Uses System.Text.Json with camelCase naming policy for consistency.
/// Includes version field for future compatibility.
/// </summary>
public sealed class SpatialSpecSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new Vector2JsonConverter(),
            new SpatialWaterTypeJsonConverter()
        }
    };

    /// <summary>
    /// Serializes a TownSpatialSpecification to JSON string.
    /// </summary>
    /// <param name="spec">The spatial specification to serialize</param>
    /// <returns>JSON string representation with version field</returns>
    public static string SerializeToJson(TownSpatialSpecification spec)
    {
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));

        var wrapper = new SpatialSpecJson
        {
            Version = 1,
            RealWorldBounds = spec.RealWorldBounds,
            BuildingPlacements = spec.BuildingPlacements,
            RoadNetwork = spec.RoadNetwork,
            WaterBodies = spec.WaterBodies,
            TerrainDescription = spec.TerrainDescription
        };

        return JsonSerializer.Serialize(wrapper, Options);
    }

    /// <summary>
    /// Deserializes a JSON string to TownSpatialSpecification.
    /// Validates version for future compatibility.
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Reconstructed TownSpatialSpecification</returns>
    /// <exception cref="ArgumentNullException">Thrown if json is null or empty</exception>
    /// <exception cref="JsonException">Thrown if JSON is invalid or incompatible version</exception>
    public static TownSpatialSpecification DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentNullException(nameof(json), "JSON string cannot be null or empty");

        try
        {
            var wrapper = JsonSerializer.Deserialize<SpatialSpecJson>(json, Options)
                ?? throw new JsonException("Failed to deserialize JSON: root element is null");

            if (wrapper.Version != 1)
                throw new JsonException($"Unsupported serialization version: {wrapper.Version}. Expected version 1.");

            if (wrapper.RealWorldBounds == null)
                throw new JsonException("RealWorldBounds cannot be null");

            if (wrapper.RoadNetwork == null)
                throw new JsonException("RoadNetwork cannot be null");

            return new TownSpatialSpecification(
                RealWorldBounds: wrapper.RealWorldBounds,
                BuildingPlacements: wrapper.BuildingPlacements ?? new Dictionary<string, BuildingPlacement>(),
                RoadNetwork: wrapper.RoadNetwork,
                WaterBodies: wrapper.WaterBodies ?? new List<SpatialWaterBody>(),
                TerrainDescription: wrapper.TerrainDescription ?? "unknown"
            );
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize JSON: {ex.Message}", ex);
        }
    }

    // Internal wrapper class for serialization with version
    private sealed class SpatialSpecJson
    {
        public int Version { get; set; }
        public BoundingBox? RealWorldBounds { get; set; }
        public Dictionary<string, BuildingPlacement>? BuildingPlacements { get; set; }
        public RoadNetwork? RoadNetwork { get; set; }
        public List<SpatialWaterBody>? WaterBodies { get; set; }
        public string? TerrainDescription { get; set; }
    }

    // Custom converter for Vector2 to support proper serialization
    private sealed class Vector2JsonConverter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token for Vector2");

            float x = 0, y = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Vector2(x, y);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    if (propertyName == "x")
                        x = reader.GetSingle();
                    else if (propertyName == "y")
                        y = reader.GetSingle();
                }
            }

            throw new JsonException("Unexpected end of JSON stream reading Vector2");
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteEndObject();
        }
    }

    // Custom converter for SpatialWaterType enum
    private sealed class SpatialWaterTypeJsonConverter : JsonConverter<SpatialWaterType>
    {
        public override SpatialWaterType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                return Enum.TryParse<SpatialWaterType>(stringValue, ignoreCase: true, out var result)
                    ? result
                    : throw new JsonException($"Unknown water type: {stringValue}");
            }

            throw new JsonException("Expected string token for SpatialWaterType");
        }

        public override void Write(Utf8JsonWriter writer, SpatialWaterType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

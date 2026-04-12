using System.Text.Json;
using System.Text.Json.Serialization;
using Oravey2.Core.World;
using Xunit;

namespace Oravey2.Tests.World;

public class LinearFeatureTypeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(LinearFeatureType.Path)]
    [InlineData(LinearFeatureType.Residential)]
    [InlineData(LinearFeatureType.Tertiary)]
    [InlineData(LinearFeatureType.Secondary)]
    [InlineData(LinearFeatureType.Primary)]
    [InlineData(LinearFeatureType.Trunk)]
    [InlineData(LinearFeatureType.Motorway)]
    [InlineData(LinearFeatureType.Rail)]
    [InlineData(LinearFeatureType.Stream)]
    [InlineData(LinearFeatureType.River)]
    [InlineData(LinearFeatureType.Canal)]
    [InlineData(LinearFeatureType.Pipeline)]
    public void SerializeDeserialize_JsonString_RoundTrips(LinearFeatureType value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<LinearFeatureType>(json, JsonOptions);

        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void Motorway_GreaterThan_Residential()
    {
        Assert.True(LinearFeatureType.Motorway > LinearFeatureType.Residential);
    }

    [Theory]
    [InlineData(LinearFeatureType.Path, (byte)0)]
    [InlineData(LinearFeatureType.Residential, (byte)1)]
    [InlineData(LinearFeatureType.Tertiary, (byte)2)]
    [InlineData(LinearFeatureType.Secondary, (byte)3)]
    [InlineData(LinearFeatureType.Primary, (byte)4)]
    [InlineData(LinearFeatureType.Trunk, (byte)5)]
    [InlineData(LinearFeatureType.Motorway, (byte)6)]
    [InlineData(LinearFeatureType.Rail, (byte)10)]
    [InlineData(LinearFeatureType.Stream, (byte)20)]
    [InlineData(LinearFeatureType.River, (byte)21)]
    [InlineData(LinearFeatureType.Canal, (byte)22)]
    [InlineData(LinearFeatureType.Pipeline, (byte)30)]
    public void CastToByte_ReturnsExpectedValue(LinearFeatureType value, byte expected)
    {
        Assert.Equal(expected, (byte)value);
    }
}

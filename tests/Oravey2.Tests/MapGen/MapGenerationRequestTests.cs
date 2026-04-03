using Oravey2.MapGen.Models;

namespace Oravey2.Tests.MapGen;

public class MapGenerationRequestTests
{
    [Fact]
    public void MapGenerationRequest_AllRequiredFields_NoException()
    {
        var request = new MapGenerationRequest
        {
            LocationName = "Portland",
            GeographyDescription = "Pacific NW river city",
            PostApocContext = "Nuclear fallout zone",
            ChunksWide = 4,
            ChunksHigh = 4,
            MinLevel = 1,
            MaxLevel = 5,
            DifficultyDescription = "Moderate",
            Factions = new[] { "Settlers", "Raiders" }
        };

        Assert.Equal("Portland", request.LocationName);
        Assert.Equal(4, request.ChunksWide);
    }

    [Fact]
    public void MapGenerationRequest_DefaultModel_IsNull()
    {
        var request = new MapGenerationRequest
        {
            LocationName = "Test",
            GeographyDescription = "Test",
            PostApocContext = "Test",
            ChunksWide = 1,
            ChunksHigh = 1,
            MinLevel = 1,
            MaxLevel = 1,
            DifficultyDescription = "Easy",
            Factions = Array.Empty<string>()
        };

        Assert.Null(request.Model);
    }

    [Fact]
    public void MapGenerationRequest_DefaultTimeOfDay_IsDawn()
    {
        var request = new MapGenerationRequest
        {
            LocationName = "Test",
            GeographyDescription = "Test",
            PostApocContext = "Test",
            ChunksWide = 1,
            ChunksHigh = 1,
            MinLevel = 1,
            MaxLevel = 1,
            DifficultyDescription = "Easy",
            Factions = Array.Empty<string>()
        };

        Assert.Equal("Dawn", request.TimeOfDay);
    }

    [Fact]
    public void MapGenerationRequest_DefaultWeather_IsOvercast()
    {
        var request = new MapGenerationRequest
        {
            LocationName = "Test",
            GeographyDescription = "Test",
            PostApocContext = "Test",
            ChunksWide = 1,
            ChunksHigh = 1,
            MinLevel = 1,
            MaxLevel = 1,
            DifficultyDescription = "Easy",
            Factions = Array.Empty<string>()
        };

        Assert.Equal("overcast", request.WeatherDefault);
    }
}

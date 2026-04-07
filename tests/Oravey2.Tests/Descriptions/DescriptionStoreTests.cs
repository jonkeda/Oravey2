using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;
using Oravey2.Core.Descriptions;
using Oravey2.Core.World;

namespace Oravey2.Tests.Descriptions;

public class DescriptionStoreTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly WorldMapStore _store;

    public DescriptionStoreTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _store = new WorldMapStore(_conn);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        _store.InsertDescription(42, "town", "A dusty settlement.", summary: "Extended description.");

        var desc = _store.GetDescription(42);

        Assert.NotNull(desc);
        Assert.Equal(42, desc.LocationId);
        Assert.Equal(LocationType.Town, desc.Type);
        Assert.Equal("A dusty settlement.", desc.Tagline);
        Assert.Equal("Extended description.", desc.Summary);
        Assert.Null(desc.Dossier);
    }

    [Fact]
    public async Task CacheHit_DoesNotCallLlm()
    {
        // Pre-populate the cache
        _store.InsertDescription(10, "town", "Tagline here.", summary: "Cached summary.");

        int llmCallCount = 0;
        var llm = new Func<string, CancellationToken, Task<string>>((p, ct) =>
        {
            llmCallCount++;
            return Task.FromResult("LLM-generated text");
        });

        var generator = new DescriptionGenerator(llm);
        var service = new DescriptionService(_store, generator);

        var context = new LocationContext("TestTown", "town", BiomeType.Wasteland);
        var summary = await service.GetOrGenerateSummaryAsync(10, context);

        Assert.Equal("Cached summary.", summary);
        Assert.Equal(0, llmCallCount);
    }

    [Fact]
    public async Task Invalidate_RegeneratesOnNextRequest()
    {
        // Pre-populate the cache
        _store.InsertDescription(20, "town", "Old tagline.", summary: "Old summary.");

        int llmCallCount = 0;
        var llm = new Func<string, CancellationToken, Task<string>>((p, ct) =>
        {
            llmCallCount++;
            return Task.FromResult("Fresh summary.");
        });

        var generator = new DescriptionGenerator(llm);
        var service = new DescriptionService(_store, generator);

        // Invalidate
        service.Invalidate(20);
        Assert.Null(_store.GetDescription(20));

        // Next request should call LLM
        var context = new LocationContext("TestTown", "town", BiomeType.Wasteland, ExistingTagline: "Old tagline.");
        var summary = await service.GetOrGenerateSummaryAsync(20, context);

        Assert.Equal("Fresh summary.", summary);
        Assert.Equal(1, llmCallCount);
    }

    [Fact]
    public void UpdateSummary_PersistsNewText()
    {
        _store.InsertDescription(30, "poi", "A ruined checkpoint.");
        _store.UpdateDescriptionSummary(30, "Updated summary text.");

        var desc = _store.GetDescription(30);
        Assert.NotNull(desc);
        Assert.Equal("Updated summary text.", desc.Summary);
        Assert.NotNull(desc.SummaryUtc);
    }

    [Fact]
    public void UpdateDossier_PersistsNewText()
    {
        _store.InsertDescription(31, "town", "A walled town.", summary: "Summary.");
        _store.UpdateDescriptionDossier(31, "Full dossier text.");

        var desc = _store.GetDescription(31);
        Assert.NotNull(desc);
        Assert.Equal("Full dossier text.", desc.Dossier);
        Assert.NotNull(desc.DossierUtc);
    }

    [Fact]
    public void GetDescription_NonExistent_ReturnsNull()
    {
        var desc = _store.GetDescription(999);
        Assert.Null(desc);
    }

    [Fact]
    public void InsertDescription_Upserts_OnDuplicateKey()
    {
        _store.InsertDescription(50, "town", "Original tagline.");
        _store.InsertDescription(50, "town", "Updated tagline.", summary: "New summary");

        var desc = _store.GetDescription(50);
        Assert.NotNull(desc);
        Assert.Equal("Updated tagline.", desc.Tagline);
        Assert.Equal("New summary", desc.Summary);
    }

    public void Dispose()
    {
        _store.Dispose();
        _conn.Dispose();
    }
}

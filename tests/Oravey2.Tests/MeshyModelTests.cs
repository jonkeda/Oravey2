using System.Text.Json;
using Oravey2.MapGen.Models.Meshy;

namespace Oravey2.Tests;

public class MeshyModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void TextTo3DRequest_Serializes_SnakeCase()
    {
        var req = new TextTo3DRequest { Mode = "refine", Prompt = "a medieval house", ArtStyle = "realistic" };
        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.Contains("\"mode\":\"refine\"", json);
        Assert.Contains("\"prompt\":\"a medieval house\"", json);
        Assert.Contains("\"art_style\":\"realistic\"", json);
    }

    [Fact]
    public void TextTo3DRequest_OmitsNulls_WhenNotSet()
    {
        var req = new TextTo3DRequest { Mode = "preview", Prompt = "a house" };
        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.DoesNotContain("art_style", json);
        Assert.DoesNotContain("should_remesh", json);
    }

    [Fact]
    public void ImageTo3DRequest_Serializes_SnakeCase()
    {
        var req = new ImageTo3DRequest { ImageUrl = "https://example.com/img.png", Prompt = "a warrior", ArtStyle = "cartoon" };
        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.Contains("\"image_url\":\"https://example.com/img.png\"", json);
        Assert.Contains("\"prompt\":\"a warrior\"", json);
        Assert.Contains("\"art_style\":\"cartoon\"", json);
    }

    [Fact]
    public void RemeshRequest_Serializes_WithPolycount()
    {
        var req = new RemeshRequest { InputTaskId = "task_123", TargetFormats = ["glb", "fbx"], TargetPolycount = 5000 };
        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.Contains("\"input_task_id\":\"task_123\"", json);
        Assert.Contains("\"target_polycount\":5000", json);
        Assert.Contains("\"target_formats\":[\"glb\",\"fbx\"]", json);
    }

    [Fact]
    public void RiggingRequest_Serializes_SnakeCase()
    {
        var req = new RiggingRequest { InputTaskId = "task_456" };
        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.Contains("\"input_task_id\":\"task_456\"", json);
    }

    [Fact]
    public void AnimationRequest_Serializes_SnakeCase()
    {
        var req = new AnimationRequest { ActionId = "walk_cycle" };
        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.Contains("\"action_id\":\"walk_cycle\"", json);
    }

    [Fact]
    public void MeshyTaskResponse_Deserializes_Result()
    {
        var json = """{"result":"task_abc123"}""";
        var resp = JsonSerializer.Deserialize<MeshyTaskResponse>(json, JsonOptions);

        Assert.NotNull(resp);
        Assert.Equal("task_abc123", resp.Result);
    }

    [Fact]
    public void MeshyTaskStatus_Deserializes_FullPayload()
    {
        var json = """
        {
            "id": "task_abc123",
            "status": "SUCCEEDED",
            "progress": 100,
            "model_urls": {
                "glb": "https://cdn.meshy.ai/model.glb",
                "fbx": "https://cdn.meshy.ai/model.fbx"
            },
            "thumbnail_url": "https://cdn.meshy.ai/thumb.png"
        }
        """;

        var status = JsonSerializer.Deserialize<MeshyTaskStatus>(json, JsonOptions);

        Assert.NotNull(status);
        Assert.Equal("task_abc123", status.Id);
        Assert.Equal("SUCCEEDED", status.Status);
        Assert.Equal(100, status.Progress);
        Assert.NotNull(status.ModelUrls);
        Assert.Equal(2, status.ModelUrls.Count);
        Assert.Equal("https://cdn.meshy.ai/model.glb", status.ModelUrls["glb"]);
        Assert.Equal("https://cdn.meshy.ai/thumb.png", status.ThumbnailUrl);
    }

    [Fact]
    public void MeshyTaskStatus_Deserializes_FailedWithError()
    {
        var json = """
        {
            "id": "task_err",
            "status": "FAILED",
            "progress": 30,
            "task_error": "Insufficient credits"
        }
        """;

        var status = JsonSerializer.Deserialize<MeshyTaskStatus>(json, JsonOptions);

        Assert.NotNull(status);
        Assert.Equal("FAILED", status.Status);
        Assert.Equal("Insufficient credits", status.TaskError);
        Assert.Null(status.ModelUrls);
    }

    [Fact]
    public void MeshyTaskStatus_Deserializes_MinimalPayload()
    {
        var json = """{"id":"task_1","status":"PENDING","progress":0}""";
        var status = JsonSerializer.Deserialize<MeshyTaskStatus>(json, JsonOptions);

        Assert.NotNull(status);
        Assert.Equal("PENDING", status.Status);
        Assert.Equal(0, status.Progress);
        Assert.Null(status.ModelUrls);
        Assert.Null(status.ThumbnailUrl);
    }

    [Fact]
    public void MeshyBalance_Deserializes_Balance()
    {
        var json = """{"balance":4500}""";
        var balance = JsonSerializer.Deserialize<MeshyBalance>(json, JsonOptions);

        Assert.NotNull(balance);
        Assert.Equal(4500, balance.Balance);
    }

    [Fact]
    public void TextTo3DRequest_RoundTrip_PreservesValues()
    {
        var original = new TextTo3DRequest { Mode = "refine", Prompt = "stone castle", ArtStyle = "pbr", ShouldRemesh = true };
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TextTo3DRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Mode, deserialized.Mode);
        Assert.Equal(original.Prompt, deserialized.Prompt);
        Assert.Equal(original.ArtStyle, deserialized.ArtStyle);
        Assert.Equal(original.ShouldRemesh, deserialized.ShouldRemesh);
    }

    [Fact]
    public void MeshyProgress_CreatesCorrectly()
    {
        var progress = new MeshyProgress { Phase = MeshyPhase.Processing, Message = "Working...", PercentComplete = 42 };

        Assert.Equal(MeshyPhase.Processing, progress.Phase);
        Assert.Equal("Working...", progress.Message);
        Assert.Equal(42, progress.PercentComplete);
    }

    [Fact]
    public void MeshyProgress_AllowsNullPercent()
    {
        var progress = new MeshyProgress { Phase = MeshyPhase.Submitting, Message = "Starting" };

        Assert.Null(progress.PercentComplete);
    }
}

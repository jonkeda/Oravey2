using System.Text.Json;
using System.Text.RegularExpressions;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Models;

namespace Oravey2.MapGen.Services;

public sealed class BlueprintCollector
{
    public event Action<GenerationProgress>? OnProgress;

    public GenerationResult CollectFromResponse(string fullResponse, TimeSpan elapsed, string? sessionId = null)
    {
        OnProgress?.Invoke(new GenerationProgress
        {
            Phase = GenerationPhase.Validating,
            Message = "Extracting blueprint JSON from response..."
        });

        var json = ExtractJson(fullResponse);

        if (json is null)
        {
            return new GenerationResult
            {
                Success = false,
                ErrorMessage = "No JSON found in the response.",
                RawJson = fullResponse,
                Elapsed = elapsed,
                SessionId = sessionId
            };
        }

        MapBlueprint? blueprint;
        try
        {
            blueprint = BlueprintLoader.LoadFromString(json);
        }
        catch (Exception ex)
        {
            return new GenerationResult
            {
                Success = false,
                ErrorMessage = $"Failed to deserialize blueprint: {ex.Message}",
                RawJson = json,
                Elapsed = elapsed,
                SessionId = sessionId
            };
        }

        OnProgress?.Invoke(new GenerationProgress
        {
            Phase = GenerationPhase.Complete,
            Message = "Blueprint extracted successfully."
        });

        return new GenerationResult
        {
            Success = true,
            Blueprint = blueprint,
            RawJson = json,
            Elapsed = elapsed,
            SessionId = sessionId
        };
    }

    internal static string? ExtractJson(string response)
    {
        // Try code fence first: ```json ... ```
        var fenceMatch = Regex.Match(response, @"```json\s*\n([\s\S]*?)\n\s*```", RegexOptions.None, TimeSpan.FromSeconds(5));
        if (fenceMatch.Success)
            return fenceMatch.Groups[1].Value.Trim();

        // Try bare JSON object
        int braceStart = response.IndexOf('{');
        if (braceStart < 0)
            return null;

        int depth = 0;
        for (int i = braceStart; i < response.Length; i++)
        {
            if (response[i] == '{') depth++;
            else if (response[i] == '}') depth--;

            if (depth == 0)
                return response[braceStart..(i + 1)];
        }

        return null;
    }
}

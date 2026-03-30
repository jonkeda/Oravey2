namespace Oravey2.Core.Audio;

public enum MusicLayer
{
    Base,
    Exploration,
    Tension,
    Combat,
    Eerie
}

/// <summary>
/// Metadata for a music layer: trigger description and crossfade duration.
/// </summary>
public sealed record MusicLayerDefinition(MusicLayer Layer, float FadeDurationSeconds)
{
    /// <summary>
    /// All layer definitions keyed by MusicLayer.
    /// Fade times from Step 09 spec: Base=0, Exploration=2, Tension=1.5, Combat=0.5, Eerie=3.
    /// </summary>
    public static IReadOnlyDictionary<MusicLayer, MusicLayerDefinition> All { get; } =
        new Dictionary<MusicLayer, MusicLayerDefinition>
        {
            [MusicLayer.Base] = new(MusicLayer.Base, 0f),
            [MusicLayer.Exploration] = new(MusicLayer.Exploration, 2f),
            [MusicLayer.Tension] = new(MusicLayer.Tension, 1.5f),
            [MusicLayer.Combat] = new(MusicLayer.Combat, 0.5f),
            [MusicLayer.Eerie] = new(MusicLayer.Eerie, 3f),
        };

    /// <summary>
    /// Returns the fade duration for the given layer.
    /// </summary>
    public static float GetFadeDuration(MusicLayer layer)
        => All.TryGetValue(layer, out var def) ? def.FadeDurationSeconds : 0f;
}

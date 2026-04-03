namespace Oravey2.Core.World.Rendering;

public enum QualityPreset { Low, Medium, High }

public sealed class QualitySettings
{
    public QualityPreset Preset { get; set; } = QualityPreset.Medium;
    public bool SubTileAssembly { get; set; } = true;
    public bool EdgeJitter { get; set; } = true;
    public float DetailDensity { get; set; } = 0.5f;
    public float DetailRange { get; set; } = 10f;
    public int LodRings { get; set; } = 2;

    public static QualitySettings FromPreset(QualityPreset preset) => preset switch
    {
        QualityPreset.Low => new()
        {
            Preset = QualityPreset.Low,
            SubTileAssembly = false,
            EdgeJitter = false,
            DetailDensity = 0,
            DetailRange = 0,
            LodRings = 1
        },
        QualityPreset.Medium => new()
        {
            Preset = QualityPreset.Medium,
            SubTileAssembly = true,
            EdgeJitter = true,
            DetailDensity = 0.5f,
            DetailRange = 10,
            LodRings = 2
        },
        QualityPreset.High => new()
        {
            Preset = QualityPreset.High,
            SubTileAssembly = true,
            EdgeJitter = true,
            DetailDensity = 1.0f,
            DetailRange = 20,
            LodRings = 3
        },
        _ => new()
    };
}

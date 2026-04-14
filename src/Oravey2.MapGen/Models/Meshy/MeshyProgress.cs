namespace Oravey2.MapGen.Models.Meshy;

public sealed class MeshyProgress
{
    public MeshyPhase Phase { get; set; }
    public string Message { get; set; } = "";
    public int? PercentComplete { get; set; }
}

public enum MeshyPhase
{
    Submitting,
    Pending,
    Processing,
    Downloading,
    Complete,
    Error
}

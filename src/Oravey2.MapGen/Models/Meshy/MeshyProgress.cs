namespace Oravey2.MapGen.Models.Meshy;

public sealed record MeshyProgress(MeshyPhase Phase, string Message, int? PercentComplete);

public enum MeshyPhase
{
    Submitting,
    Pending,
    Processing,
    Downloading,
    Complete,
    Error
}

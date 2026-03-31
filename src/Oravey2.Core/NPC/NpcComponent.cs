using Stride.Engine;

namespace Oravey2.Core.NPC;

public class NpcComponent : EntityComponent
{
    public NpcDefinition? Definition { get; set; }
    public float CollisionRadius { get; set; } = 0.4f;
}

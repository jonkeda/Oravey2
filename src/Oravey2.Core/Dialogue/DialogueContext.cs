namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.World;

public sealed class DialogueContext
{
    public SkillsComponent Skills { get; }
    public InventoryComponent Inventory { get; }
    public WorldStateService WorldState { get; }
    public LevelComponent Level { get; }
    public IEventBus EventBus { get; }

    public DialogueContext(
        SkillsComponent skills,
        InventoryComponent inventory,
        WorldStateService worldState,
        LevelComponent level,
        IEventBus eventBus)
    {
        Skills = skills;
        Inventory = inventory;
        WorldState = worldState;
        Level = level;
        EventBus = eventBus;
    }
}

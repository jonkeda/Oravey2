namespace Oravey2.Core.Quests;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.World;

public sealed class QuestContext
{
    public InventoryComponent Inventory { get; }
    public WorldStateService WorldState { get; }
    public LevelComponent Level { get; }
    public QuestLogComponent QuestLog { get; }
    public IEventBus EventBus { get; }

    public QuestContext(
        InventoryComponent inventory,
        WorldStateService worldState,
        LevelComponent level,
        QuestLogComponent questLog,
        IEventBus eventBus)
    {
        Inventory = inventory;
        WorldState = worldState;
        Level = level;
        QuestLog = questLog;
        EventBus = eventBus;
    }
}

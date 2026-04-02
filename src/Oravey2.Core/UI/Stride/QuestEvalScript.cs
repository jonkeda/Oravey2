using Oravey2.Core.Framework.State;
using Oravey2.Core.Quests;
using Stride.Engine;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// SyncScript that evaluates all active quests each frame.
/// Placed in both town and wasteland scenes to enable real-time quest progression.
/// </summary>
public class QuestEvalScript : SyncScript
{
    public QuestProcessor? Processor { get; set; }
    public QuestLogComponent? QuestLog { get; set; }
    public QuestContext? Context { get; set; }
    public GameStateManager? StateManager { get; set; }

    public override void Update()
    {
        if (Processor == null || QuestLog == null || Context == null)
            return;

        // Only evaluate during Exploring state (not during menus/combat)
        if (StateManager != null &&
            StateManager.CurrentState != GameState.Exploring &&
            StateManager.CurrentState != GameState.InCombat &&
            StateManager.CurrentState != GameState.InDialogue)
            return;

        foreach (var def in QuestChainDefinitions.All)
        {
            if (QuestLog.GetStatus(def.Id) == QuestStatus.Active)
                Processor.EvaluateQuest(QuestLog, def, Context);
        }
    }
}

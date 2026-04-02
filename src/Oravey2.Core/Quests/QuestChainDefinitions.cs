namespace Oravey2.Core.Quests;

/// <summary>
/// Defines the 3-quest chain for M1 Phase 3: Rat Problem → Clear the Camp → Safe Passage.
/// </summary>
public static class QuestChainDefinitions
{
    /// <summary>
    /// Quest 1: Kill 3 radrats in the wasteland, then report to Elder Tomas.
    /// Two stages: kill_rats (auto-advances on 3 kills) → report_1 (completed via dialogue).
    /// </summary>
    public static QuestDefinition RatHunt() => new(
        Id: "q_rat_hunt",
        Title: "Rat Problem",
        Description: "Kill 3 radrats in the Scorched Outskirts.",
        Type: QuestType.Side,
        FirstStageId: "kill_rats",
        Stages: new Dictionary<string, QuestStage>
        {
            ["kill_rats"] = new(
                Id: "kill_rats",
                Description: "Kill 3 radrats (0/3)",
                Conditions: [new QuestCounterCondition("rats_killed", 3)],
                OnCompleteActions: [new QuestSetFlagAction("rats_cleared")],
                NextStageId: "report_1",
                FailConditions: [],
                OnFailActions: []),
            ["report_1"] = new(
                Id: "report_1",
                Description: "Report to Elder Tomas in Haven",
                Conditions: [new QuestFlagCondition("q_rat_hunt_reported")],
                OnCompleteActions: [new QuestSetFlagAction("q_rat_hunt_done")],
                NextStageId: null,
                FailConditions: [],
                OnFailActions: []),
        },
        XPReward: 50);

    /// <summary>
    /// Quest 2: Kill the raider leader Scar at the eastern ruins.
    /// Single stage: auto-completes when scar_killed flag is set.
    /// On completion, auto-starts Quest 3.
    /// </summary>
    public static QuestDefinition RaiderCamp() => new(
        Id: "q_raider_camp",
        Title: "Clear the Camp",
        Description: "Kill the raider leader Scar at the eastern ruins.",
        Type: QuestType.Side,
        FirstStageId: "kill_scar",
        Stages: new Dictionary<string, QuestStage>
        {
            ["kill_scar"] = new(
                Id: "kill_scar",
                Description: "Kill the raider leader Scar",
                Conditions: [new QuestFlagCondition("scar_killed")],
                OnCompleteActions:
                [
                    new QuestSetFlagAction("q_raider_camp_done"),
                    new QuestStartQuestAction("q_safe_passage"),
                ],
                NextStageId: null,
                FailConditions: [],
                OnFailActions: []),
        },
        XPReward: 100);

    /// <summary>
    /// Quest 3: Return to Elder Tomas and report success.
    /// Single stage: completed when reported_to_elder flag is set via dialogue.
    /// </summary>
    public static QuestDefinition SafePassage() => new(
        Id: "q_safe_passage",
        Title: "Safe Passage",
        Description: "Return to Elder Tomas and report your success.",
        Type: QuestType.Main,
        FirstStageId: "report_back",
        Stages: new Dictionary<string, QuestStage>
        {
            ["report_back"] = new(
                Id: "report_back",
                Description: "Report to Elder Tomas in Haven",
                Conditions: [new QuestFlagCondition("reported_to_elder")],
                OnCompleteActions: [new QuestSetFlagAction("m1_complete")],
                NextStageId: null,
                FailConditions: [],
                OnFailActions: []),
        },
        XPReward: 150);

    public static QuestDefinition? GetQuest(string questId) => questId switch
    {
        "q_rat_hunt" => RatHunt(),
        "q_raider_camp" => RaiderCamp(),
        "q_safe_passage" => SafePassage(),
        _ => null,
    };

    public static IReadOnlyList<QuestDefinition> All => [RatHunt(), RaiderCamp(), SafePassage()];
}

# Step 5 — Dialogue & Quests

**Goal:** Branching dialogue system and data-driven quest engine.

**Depends on:** Steps 1, 2

---

## Deliverables

1. Dialogue data model: `DialogueTree` → `DialogueNode` → `DialogueChoice` (JSON-loaded)
2. `DialogueComponent` — attached to NPCs, references dialogue tree ID
3. `DialogueProcessor` — manages active conversation: display node text, present choices, evaluate conditions
4. Skill checks in dialogue: `SkillCheckCondition` (skill, threshold, visible/hidden)
5. Dialogue consequences: `ConsequenceAction` list per choice (give item, modify faction rep, set flag)
6. Quest data model: `QuestDefinition` → `QuestStage` → conditions + actions (JSON-loaded)
7. `QuestLogComponent` — tracks active/completed/failed quests and current stages
8. `QuestProcessor` — evaluates stage conditions each frame, triggers actions on completion
9. World flags system: `WorldStateService` — dictionary of string→bool for tracking global state
10. `DialogueUI` — text display, choice buttons, skill check indicators (placeholder visuals)
11. Unit tests for dialogue traversal, quest stage evaluation, world flag conditions

---

## Data Format Example

```json
{
  "id": "merchant_intro",
  "nodes": [
    {
      "id": "start",
      "speaker": "Merchant",
      "text": "What do you want, stranger?",
      "choices": [
        { "text": "I need supplies.", "next": "trade" },
        { "text": "[Speech 40] Give me a discount.", "next": "discount_check",
          "condition": { "type": "skill_check", "skill": "Speech", "threshold": 40 } },
        { "text": "Goodbye.", "next": null }
      ]
    }
  ]
}
```

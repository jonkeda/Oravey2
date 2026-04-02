# UI Tests Task List

Tracking pass/fail status for all UI tests in `tests/Oravey2.UITests/`.
Last updated after Phase 4 test cleanup (56 redundant/converted tests removed, 3 files deleted).

## CameraDefaultTests (1 test) ✅
- [x] PlayerVisible_AtTunedZoom

## CameraFollowTests (5 tests) ✅
- [x] PlayerOnScreen_AtStart
- [x] PlayerOnScreen_AfterMovement
- [x] CameraOffset_MatchesYawPitch
- [x] CameraFollows_PositionDelta
- [x] PlayerVisible_FromAllFourRotations

## CameraRotationTests (4 tests) ✅
- [x] QHold_WorldRotatesOnScreen
- [x] EHold_WorldRotatesOpposite
- [x] FullRotation_Returns360
- [x] RotationChangesView_Visually

## CombatBalanceTests (1 test) ✅
- [x] FullCombat_PlayerSurvives

## CombatGameplayTests (5 tests) ✅
- [x] PlayerCanAttack_DamagesEnemy
- [x] EnemiesAttackPlayer_OverTime
- [x] KillEnemy_RemovesFromList
- [x] KillEnemy_EntityRemovedFromScene
- [x] AllEnemiesDead_CombatStateReset

## CombatTriggerTests (3 tests) ✅
- [x] PlayerAtOrigin_NoCombat
- [x] TeleportNearEnemy1_TriggersCombat
- [x] TeleportFarFromEnemies_StaysExploring

## DeterministicCombatTests (4 tests) ✅
- [x] Player_Beats_SingleWeakEnemy
- [x] Player_Dies_Against_OverpoweredEnemy
- [x] ThreeEnemyFight_PlayerSurvives
- [x] ArmorReducesDamage_InScenario

## EnemyHpBarTests (5 tests) ✅
- [x] EnemyBars_NotVisible_WhenExploring
- [x] EnemyBars_Visible_WhenInCombat
- [x] EnemyBars_ShowAllLivingEnemies
- [x] EnemyBars_MatchCombatState_Hp
- [x] EnemyBars_RemoveDeadEnemy

## FullscreenTests (1 test — SKIPPED)
- [ ] F11_TogglesFullscreen *(Skip: Requires GetWindowState automation query)*

## GameLifecycleTests (5 tests) ✅
- [x] Game_StartsAndConnects
- [x] Game_IsNotBusy_AfterStartup
- [x] Game_IsInExploringState
- [x] Game_PlayerEntityExists
- [x] Game_CameraEntityExists

## GameOverTests (3 tests) ✅
- [x] GameOverOverlay_NotVisible_AtStart
- [x] PlayerDeath_TransitionsToGameOver
- [x] PlayerDeath_ShowsGameOverOverlay

## GameWorldTests (2 tests) ✅
- [x] GameWorld_SceneHasRenderableEntities
- [x] GameWorld_ScreenshotIsNotSolidColor

## HudStateTests (2 tests) ✅
- [x] HudState_ShowsInCombat_WhenFighting
- [x] HudState_HealthDecreases_InCombat

## InputFreezeTests (3 tests) ✅
- [x] Movement_Blocked_DuringGameOver
- [x] InventoryToggle_Blocked_DuringGameOver
- [x] GameState_StaysGameOver_AfterInput

## InventoryOverlayTests (3 tests) ✅
- [x] Overlay_NotVisible_AtStart
- [x] TabPress_OpensOverlay
- [x] TabPress_ClosesOverlay

## LootDropTests (4 tests) ✅
- [x] KillEnemy_SpawnsLootCube
- [x] LootCube_AtEnemyPosition
- [x] LootCube_HasItems
- [x] MultipleLootCubes_FromMultipleKills

## LootPickupTests (4 tests) ✅
- [x] WalkOverLoot_PicksUpItems
- [x] WalkOverLoot_RemovesLootEntity
- [x] PickupAdds_ToExistingStacks
- [x] Inventory_WeightIncreases_AfterPickup

## MenuSaveLoadTests (4 tests) ✅
- [x] PauseMenu_NotVisible_Initially
- [x] PauseMenu_EscapeOpens
- [x] SaveLoad_RoundTrip_Position
- [x] PauseMenu_SaveGame_ShowsNotification

## NotificationFeedTests (2 tests) ✅
- [x] LootPickup_ShowsNotification
- [x] Notifications_HavePositiveTimeRemaining

## QuestChainTests (7 tests) ✅
- [x] AcceptQuest1_FromElder
- [x] KillCounter_Increments_ViaAutomation
- [x] Quest1_Complete_After3Kills_SetsRatsCleared
- [x] Quest1_Reward_OnReturn
- [x] Quest2_Available_AfterQuest1
- [x] Quest2_Accept_SetsActive
- [x] Quest3_ReportToElder_Completes

## QuickSaveDeathPenaltyTests (4 tests) ✅
- [x] QuickSave_CreatesFile
- [x] QuickSaveLoad_RestoresPosition
- [x] DeathPenalty_LosesCaps
- [x] SaveLoad_PreservesCaps

## ScenarioResetTests (4 tests) ✅
- [x] ResetScenario_RemovesAllEnemies
- [x] ResetScenario_HealsPlayerToMax
- [x] ResetScenario_ExitsCombat
- [x] ResetAfterGameOver_RestoresToExploring

## ScenarioSpawnTests (4 tests) ✅
- [x] SpawnEnemy_CreatesEnemyAtPosition
- [x] SpawnEnemy_CustomWeaponStats
- [x] SpawnEnemy_MultipleEnemies
- [x] SetPlayerWeapon_EquipsCustomWeapon

## SpatialMovementTests (6 tests) ✅
- [x] MoveW_PlayerMovesToExpectedTile
- [x] MoveS_PlayerMovesOpposite
- [x] MoveA_PlayerMovesLeftOnScreen
- [x] MoveD_PlayerMovesRightOnScreen
- [x] MoveW_PlayerStaysOnMap
- [x] WASD_AreOrthogonal

## TownTests (14 tests) ✅
- [x] Town_LoadsSuccessfully
- [x] Town_TeleportToElder_ShowsInRange
- [x] Town_FarFromNpc_NotInRange
- [x] Town_InteractWithElder_FiresEvent
- [x] Town_InteractWithElder_OpensDialogue
- [x] Town_DialogueShows_SpeakerAndText
- [x] Town_DialogueChoices_ArePresent
- [x] Town_SelectLeave_EndsDialogue
- [x] Town_CivilianDialogue_Works
- [x] Town_BuyMedkit_DeductsCaps
- [x] Town_BuyMedkit_AddsToInventory
- [x] Town_SellScrap_AddsCaps
- [x] Town_TeleportToGate_TransitionsZone
- [x] Town_ZoneTransition_PlayerAtSpawn

## VictoryTests (4 tests) ✅
- [x] KillAllEnemies_TransitionsToExploring
- [x] KillAllEnemies_ShowsVictoryOverlay
- [x] VictoryOverlay_AutoDismisses
- [x] VictoryOverlay_EnemyBarsHide

## WallCollisionTests (6 tests) ✅
- [x] PlayerCannot_WalkPastNorthWall
- [x] PlayerCannot_WalkPastSouthWall
- [x] PlayerCannot_WalkPastEastWall
- [x] PlayerCannot_WalkPastWestWall
- [x] PlayerSlides_AlongWallDiagonal
- [x] PlayerOnWalkableTile_AfterCollision

## WastelandTests (1 test) ✅
- [x] Wasteland_GateReturnsToTown

## ZoomTests (3 tests) ✅
- [x] InitialZoom_WorldEdgesVisible
- [x] ZoomOut_ShowsMoreWorld
- [x] ZoomIn_ReducesZoom

---
**Total: 114 tests (113 active + 1 skipped) across 29 files**

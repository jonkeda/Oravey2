# UI Tests Task List

Tracking pass/fail status for all UI tests in `tests/Oravey2.UITests/`.

## GameLifecycleTests (5 tests) ✅
- [x] Game_StartsAndConnects
- [x] Game_IsNotBusy_AfterStartup
- [x] Game_IsInExploringState
- [x] Game_PlayerEntityExists
- [x] Game_CameraEntityExists

## GameWorldTests (5 tests) ✅
- [x] GameWorld_IsReady
- [x] GameWorld_PlayerStartsNearOrigin
- [x] GameWorld_StateIsExploring
- [x] GameWorld_SceneHasRenderableEntities
- [x] GameWorld_ScreenshotIsNotSolidColor

## CameraFollowTests (5 tests) ✅
- [x] PlayerOnScreen_AtStart
- [x] PlayerOnScreen_AfterMovement
- [x] CameraOffset_MatchesYawPitch
- [x] CameraFollows_PositionDelta
- [x] PlayerVisible_FromAllFourRotations

## CameraRotationTests (5 tests) ✅
- [x] QHold_WorldRotatesOnScreen
- [x] EHold_WorldRotatesOpposite
- [x] FullRotation_Returns360
- [x] RotationChangesView_Visually
- [x] Rotation_LandmarkMovement

## ZoomTests (4 tests) ✅
- [x] InitialZoom_WorldEdgesVisible
- [x] ZoomState_IsQueryable
- [x] ZoomOut_ShowsMoreWorld
- [x] ZoomIn_ReducesZoom

## SpatialMovementTests (6 tests) ✅
- [x] MoveW_PlayerMovesToExpectedTile
- [x] MoveS_PlayerMovesOpposite
- [x] MoveA_PlayerMovesLeftOnScreen
- [x] MoveD_PlayerMovesRightOnScreen
- [x] MoveW_PlayerStaysOnMap
- [x] WASD_AreOrthogonal

## FullscreenTests (1 test — SKIPPED)
- [ ] F11_TogglesFullscreen *(Skip: Requires GetWindowState automation query)*

## CombatTriggerTests (7 tests) ✅
- [x] StartState_IsExploring
- [x] ThreeEnemies_ExistAtStartup
- [x] AllEnemies_InsideMapBounds
- [x] PlayerAtOrigin_NoCombat
- [x] TeleportNearEnemy1_TriggersCombat
- [x] TeleportFarFromEnemies_StaysExploring
- [x] CombatState_ShowsInCombat

## CombatGameplayTests (6 tests) ✅
- [x] PlayerCanAttack_DamagesEnemy
- [x] EnemiesAttackPlayer_OverTime
- [x] KillEnemy_RemovesFromList
- [x] KillEnemy_EntityRemovedFromScene
- [x] AllEnemiesDead_ReturnsToExploring
- [x] AllEnemiesDead_CombatStateReset

## WallCollisionTests (6 tests) ✅
- [x] PlayerCannot_WalkPastNorthWall
- [x] PlayerCannot_WalkPastSouthWall
- [x] PlayerCannot_WalkPastEastWall
- [x] PlayerCannot_WalkPastWestWall
- [x] PlayerSlides_AlongWallDiagonal
- [x] PlayerOnWalkableTile_AfterCollision

## StartingInventoryTests (5 tests) ✅
- [x] PlayerHas_PipeWrenchEquipped
- [x] PlayerHas_MedkitsInInventory
- [x] StartingWeight_IsCorrect
- [x] NotOverweight_AtStart
- [x] EquipmentSlots_MostlyEmpty

## LootDropTests (5 tests) ✅
- [x] NoLootEntities_AtStart
- [x] KillEnemy_SpawnsLootCube
- [x] LootCube_AtEnemyPosition
- [x] LootCube_HasItems
- [x] MultipleLootCubes_FromMultipleKills

## LootPickupTests (4 tests) ✅
- [x] WalkOverLoot_PicksUpItems
- [x] WalkOverLoot_RemovesLootEntity
- [x] PickupAdds_ToExistingStacks
- [x] Inventory_WeightIncreases_AfterPickup

## HudStateTests (6 tests) ✅
- [x] HudState_HasFullHealth_AtStart
- [x] HudState_ShowsExploring_AtStart
- [x] HudState_ShowsLevel1_AtStart
- [x] HudState_ApMatches_MaxAp
- [x] HudState_ShowsInCombat_WhenFighting
- [x] HudState_HealthDecreases_InCombat

## InventoryOverlayTests (5 tests) ✅
- [x] Overlay_NotVisible_AtStart
- [x] TabPress_OpensOverlay
- [x] TabPress_ClosesOverlay
- [x] Overlay_ShowsStartingItems
- [x] Overlay_ShowsCorrectWeight

---
**Total: 75 tests (74 active + 1 skipped)**

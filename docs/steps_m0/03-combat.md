# Step 3 — Combat

**Goal:** Real-time with pause combat loop, AP system, damage model, cover, and basic combat UI.

**Depends on:** Steps 1, 2

---

## Deliverables

1. `CombatComponent` — AP current/max, AP regen rate, in-combat flag
2. `WeaponComponent` — damage, range, AP cost, accuracy, ammo type, fire rate
3. `ArmorComponent` — damage reduction, coverage zones
4. `CoverComponent` — attached to world objects, provides cover value (half/full)
5. `CombatProcessor` — RTwP loop: processes actions in real time, handles pause, resolves attacks
6. Damage resolution pipeline: hit roll → location → weapon damage × modifiers − armor → apply
7. Action queue: player queues actions during pause, executed in order when unpaused
8. `CombatStateManager` — transitions: Exploring ↔ InCombat, triggers on aggro/all-enemies-dead
9. Basic combat HUD: HP bar, AP bar, pause indicator, action queue display
10. Projectile system: pooled projectile entities with travel time and hit detection
11. Death/down state: entity disabled on 0 HP, loot drop for enemies

---

## Key Constants (Defaults)

| Constant | Value |
|----------|-------|
| Max AP | 10 |
| AP regen | 2/sec |
| Melee attack cost | 3 AP |
| Pistol shot cost | 2 AP |
| Rifle shot cost | 4 AP |
| Half cover | −30% to-hit for attacker |
| Full cover | −60% to-hit for attacker |

---

## Combat Flow

```
1. Enemy enters aggro range → GameState → InCombat
2. Real-time: AP regens, player/AI spend AP on actions
3. Player presses Pause → time freezes, queue actions
4. Unpause → queued actions execute
5. All enemies dead or player flees → GameState → Exploring
```

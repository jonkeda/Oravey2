---
applyTo: "tests/Oravey2.Tests/**"
description: "Guidelines for writing and maintaining Oravey2 unit tests. Use when creating, editing, or reviewing unit test files."
---

# Oravey2 Unit Test Guidelines

## When to write a unit test

Unit tests verify isolated logic without a running game process. **If you can test it by constructing a component directly and calling methods on it, it's a unit test.**

Write unit tests for:

- **Initial/default values** — HP=105, AP=10, Level=1, caps=50, camera zoom=28
- **Formulas** — health from endurance, AP from agility, damage with armor DR, XP thresholds
- **State machine transitions** — Exploring→InCombat→GameOver→Exploring
- **Data model operations** — add/remove inventory, equip/unequip, weight calculation
- **Component behavior** — quest stage progression, counter thresholds, flag setting, dialogue conditions
- **Config assertions** — weapon stats, spawn point positions, NPC definitions, camera defaults
- **Serialization** — save/load data shape round-trips

## When NOT to write a unit test

Do not use unit tests for behavior that requires the full game loop. These belong in `tests/Oravey2.UITests/`:

- Screen-space rendering (WorldToScreen, screenshots, OnScreen checks)
- Keyboard/mouse input processing (HoldKey/PressKey → state change)
- Spatial/physics interactions (collision, proximity triggers, zone transitions)
- Game process lifecycle (startup, shutdown, automation pipe connectivity)
- Cross-system integration that only fires in the live loop (kill→loot→pickup→notification)

## One canonical location per assertion

Before adding a test, grep for existing tests that check the same behavior. Each fact should be verified in exactly one place. If a unit test already covers a value (e.g., `HealthComponentTests.TakeDamage_ReducesHP`), do not add a UI test for the same thing.

## Naming convention

```
ComponentName_Action_ExpectedResult
```

Examples:
- `HealthComponent_TakeDamage_ReducesHP`
- `GameStateManager_TransitionToCombat_SetsInCombat`
- `InventoryComponent_AddItem_IncreasesWeight`
- `CombatFormulas_ArmorDR_ReducesDamage`

## Test structure

```csharp
[Fact]
public void HealthComponent_TakeDamage_ReducesHP()
{
    // Arrange
    var health = new HealthComponent(maxHp: 100);

    // Act
    health.TakeDamage(25);

    // Assert
    Assert.Equal(75, health.CurrentHP);
}
```

## Running tests

```bash
# Run all unit tests
dotnet test tests/Oravey2.Tests

# Run a specific test class
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~HealthComponentTests"

# Run a specific test
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~TakeDamage_ReducesHP"
```

## Speed target

The full unit suite should run in under 10 seconds. Unit tests must not:

- Launch external processes
- Use named pipes or network I/O
- Reference Brinell or Stride assemblies
- Use `Thread.Sleep` or real timers

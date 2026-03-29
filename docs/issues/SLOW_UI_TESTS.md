# Issue: UI Tests Are Slow

## Symptom

When running UI tests, each test has a visible pause between the last game action (e.g. player stops moving) and the game window closing. The full test suite of ~30 UI tests takes ~5 minutes.

## Root Causes

### 1. `WaitForExit(5000)` in teardown — **PRIMARY**

Every test class creates its own game process via `OraveyTestFixture`. In `StrideGameDriver.StopAsync()`:

```csharp
// Brinell/srcnew/Brinell.Stride/Infrastructure/StrideGameDriver.cs:67
_gameProcess.WaitForExit(5000);
```

After sending the `Exit` command and disconnecting the pipe, the driver blocks up to **5 seconds** waiting for the process to exit. The game likely exits in under 1 second, but `WaitForExit(int)` is a synchronous poll with no early return on exit — it waits the full duration or until the process terminates.

**This is the visible "nothing happening" pause.** Each of the ~10 test classes pays this cost.

Estimated waste: **up to 5s × 10 classes = 50s** of idle waiting.

### 2. `Thread.Sleep()` after every input action — **SECONDARY**

Every test adds 200–300ms of `Thread.Sleep()` after each `HoldKey` or `PressKey` call:

```csharp
_fixture.Context.HoldKey(VirtualKey.W, 1000);  // blocks 1000ms (game time)
Thread.Sleep(300);                                // extra 300ms for "settling"
```

These sleeps are unnecessary because `HoldKey` is synchronous — the named-pipe response is not sent until the game-side `PendingKeyRelease` timer fires and the key is released (see `AutomationGameSystem.cs:116`). By the time `HoldKey` returns, the key has already been released and the game has processed that frame.

Similarly, `PressKey` schedules a key down + deferred release via `MinKeyPressDuration`, and the TCS doesn't complete until the release fires. No additional settling time is needed.

**Thread.Sleep inventory per test file:**

| File | Total Sleep per test (typical) |
|------|-------------------------------|
| SpatialMovementTests.cs | 200–600ms |
| CameraFollowTests.cs | 300–2000ms (loop tests) |
| CameraRotationTests.cs | 300–1200ms (loop tests) |
| ZoomTests.cs | 0ms |
| FullscreenTests.cs | 0ms |

### 3. One process per test class — **STRUCTURAL**

Each test class implements `IAsyncLifetime` with its own `OraveyTestFixture`, meaning the game is launched and killed for every class. With 10 classes, that's 10 startup/teardown cycles. Startup includes process launch + pipe connection (~2–3s each).

## Proposed Fixes

### Fix 1: Reduce `WaitForExit` timeout (quick win)

In `StrideGameDriver.StopAsync`, reduce from 5000ms to 1000ms or use `Process.WaitForExitAsync` with a cancellation token:

```csharp
// Option A: shorter timeout
_gameProcess.WaitForExit(1000);

// Option B: event-driven (preferred)
using var cts = new CancellationTokenSource(2000);
try { await _gameProcess.WaitForExitAsync(cts.Token); }
catch (OperationCanceledException) { _gameProcess.Kill(); }
```

**Estimated savings: ~40s across full suite.**

### Fix 2: Remove unnecessary `Thread.Sleep()` calls

`HoldKey` already blocks until the key is released on the game thread. `PressKey` blocks until the key press cycle completes. No extra settling is needed. Remove all `Thread.Sleep(200)` / `Thread.Sleep(300)` after these calls.

**Estimated savings: ~10–15s across full suite.**

### Fix 3: Share game process across test classes (larger refactor)

Use xUnit's `ICollectionFixture<T>` to share a single game instance across all test classes:

```csharp
[CollectionDefinition("GameTests")]
public class GameTestCollection : ICollectionFixture<SharedGameFixture> { }

[Collection("GameTests")]
public class SpatialMovementTests { ... }
```

This eliminates N-1 startup/teardown cycles. Requires tests to not depend on fresh state (or add a `ResetGameState` automation command).

**Estimated savings: ~30–40s across full suite.**

## Impact

| Fix | Effort | Est. Savings |
|-----|--------|-------------|
| Reduce WaitForExit | Trivial (1 line) | ~40s |
| Remove Thread.Sleep | Low (search & delete) | ~10–15s |
| Shared fixture | Medium (refactor) | ~30–40s |
| **Total** | | **~80–95s** (from ~300s → ~210s) |

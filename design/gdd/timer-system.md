# Timer System

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Satisfying Progression (valid words visibly extend time, creating immediate feedback)

---

## Overview

The Timer System drives the secondary game mode in ReelWords: a real-time countdown
that starts at a configured duration and counts down each frame using `Time.deltaTime`.
When the player submits a valid word, the timer freezes for a configurable number of
seconds before resuming, rewarding smart play with a visible reprieve. When time
reaches zero, the system fires `OnTimerExpired` and transitions the game to the
Game Over state. The system exposes `Pause()` and `Resume()` calls used by the Pause
Menu and Game State Machine, and fires `OnLowTime` when remaining time crosses a
configurable warning threshold so the HUD can display urgency feedback.

---

## Player Fantasy

In timer mode the player should feel the pressure of a ticking clock competing with
their desire to find high-value words. When they submit a valid word, the brief freeze
should feel like a small breath of relief — a mechanical "reward exhale" before the
pressure resumes. The low-time warning is designed to spike adrenaline and push the
player to act quickly, not carefully, which creates a different strategic mode than the
final turns of turn-limit mode. The target MDA aesthetics are **Challenge** (primary)
and **Sensation** (the moment of freeze and the urgency pulse).

---

## Detailed Design

### Core Rules

1. The timer has exactly four states: `Stopped`, `Running`, `Paused`, and `Expired`.
2. The timer only counts down while in the `Running` state.
3. `TimeRemaining` can never be negative; it clamps to zero and transitions to `Expired`.
4. `TimeRemaining` can never exceed `MaxTime`; bonus time is clamped to that ceiling.
5. A valid word submission triggers the **Freeze Bonus**: the timer enters a temporary
   internal `Frozen` sub-state for `BonusFreezeSeconds`, then automatically returns
   to `Running`. The Frozen sub-state is transparent to the outside — the timer remains
   in the `Running` state from external callers' perspective. `IsFrozen` is an observable
   property for HUD animation only.
6. An invalid word submission has no effect on the timer.
7. `OnLowTime` fires exactly once per game session when `TimeRemaining` first falls at
   or below `LowTimeThreshold`. It does not re-fire on subsequent frames.
8. `OnTimerExpired` fires exactly once, on the frame `TimeRemaining` reaches zero.
9. The timer does not auto-start. The Game State Machine calls `StartTimer()` when
   the game session begins.

### States and Transitions

```
Stopped ──StartTimer()──► Running
                            │
              ┌─────────────┼─────────────┐
              │             │             │
           Pause()     ValidWord     TimeRemaining
              │         triggers        == 0
              ▼         Freeze           │
           Paused       (internal)       ▼
              │             │          Expired
           Resume()         │
              │             ▼
              └──────► Running (resumed or freeze ended)
```

**State descriptions:**

| State   | deltaTime applied | Events fire | Freeze sub-state active |
|---------|-------------------|-------------|-------------------------|
| Stopped | No                | No          | No                      |
| Running | Yes (unless Frozen) | Yes       | Possible                |
| Paused  | No                | No          | No (freeze timer pauses too) |
| Expired | No                | No          | No                      |

**Freeze sub-state detail:**
- When a valid word is submitted and the timer is `Running`:
  - `FreezeTimeRemaining = BonusFreezeSeconds`
  - `IsFrozen = true`
  - Each frame, `FreezeTimeRemaining -= Time.deltaTime` (not `TimeRemaining`)
  - When `FreezeTimeRemaining <= 0`: `IsFrozen = false`, normal countdown resumes
- If `Pause()` is called during a freeze, the freeze timer also pauses.
- On `Resume()`, both the main countdown and any active freeze resume from where they stopped.

### Interactions with Other Systems

**Board Manager** (`BoardManager.OnSubmissionAttempted`): The Timer System subscribes
to this event. When the submission result is `Valid`, it triggers the Freeze Bonus.
When the result is `Invalid`, it takes no action.

**Pause Menu**: Calls `TimerSystem.Pause()` on show and `TimerSystem.Resume()` on
dismiss via Resume button. Does not interact with the timer state directly.

**Game State Machine**: Calls `TimerSystem.StartTimer()` on entering `Playing` state.
Subscribes to `TimerSystem.OnTimerExpired` to trigger the `Playing → GameOver`
transition. Calls `TimerSystem.Pause()` on entering `Paused` state and
`TimerSystem.Resume()` on returning to `Playing`.

**Turn/Timer HUD**: Subscribes to `TimerSystem.OnTick` (fired every frame when
Running) to update the countdown display. Subscribes to `TimerSystem.OnLowTime`
to trigger the red pulse animation. Reads `TimerSystem.IsFrozen` to show the
freeze indicator.

**Game Mode Manager**: Configures the timer by passing `TimerConfig` (containing
`StartDuration`, `BonusFreezeSeconds`, `MaxTime`, `LowTimeThreshold`) to
`TimerSystem.Configure()` before `StartTimer()` is called.

---

## Formulas

### Per-Frame Countdown

Applied every `Update()` frame when state is `Running` and `IsFrozen == false`:

```
TimeRemaining = max(0, TimeRemaining - Time.deltaTime)
```

Variables:
- `TimeRemaining` (float, seconds): current time left. Range: [0, MaxTime].
- `Time.deltaTime` (float, seconds): elapsed time since last frame. Typical range: [0.008, 0.1].

If `TimeRemaining` was already 0 after this calculation, transition to `Expired`.

### Freeze Sub-State Countdown

Applied every `Update()` frame when state is `Running` and `IsFrozen == true`:

```
FreezeTimeRemaining = max(0, FreezeTimeRemaining - Time.deltaTime)
if FreezeTimeRemaining == 0: IsFrozen = false
```

Variables:
- `FreezeTimeRemaining` (float, seconds): time left in current freeze. Range: [0, BonusFreezeSeconds].

### Low Time Trigger

Evaluated once per frame when state is `Running`:

```
if (not LowTimeFired) and (TimeRemaining <= LowTimeThreshold):
    LowTimeFired = true
    fire OnLowTime(TimeRemaining)
```

Variables:
- `LowTimeFired` (bool): guard flag, reset to `false` in `StartTimer()`.
- `LowTimeThreshold` (float, seconds): default 10.0.

### Example Calculation

Configuration: `StartDuration = 120s`, `BonusFreezeSeconds = 3s`, `MaxTime = 120s`.

Player submits a valid word at `TimeRemaining = 45.6s`:
1. `IsFrozen = true`, `FreezeTimeRemaining = 3.0s`
2. Frames tick: `FreezeTimeRemaining` drops from 3.0 to 0 over ~3 seconds
3. `IsFrozen = false`, countdown resumes from `45.6s`

Player submits a valid word at `TimeRemaining = 9.2s`:
- `OnLowTime` already fired (9.2 < 10.0), so no re-fire.
- `IsFrozen = true` for 3 seconds — this prevents expiration during the freeze.
- After freeze: `TimeRemaining = 9.2s`, countdown resumes.

---

## Edge Cases

**Submission during freeze**: If a second valid word is submitted while `IsFrozen == true`,
reset `FreezeTimeRemaining = BonusFreezeSeconds`. This extends the freeze but does not
stack freezes. Rationale: stacking freezes would allow infinite time extension chains
in rapid-fire edge cases; resetting is simpler and still feels rewarding.

**Pause during freeze**: Both `TimeRemaining` and `FreezeTimeRemaining` stop advancing.
On resume, both continue from their paused values. The freeze duration is not "lost"
during a pause.

**`StartTimer()` called on a non-Stopped timer**: Log a warning and re-initialize.
This handles the edge case where a scene reload occurs mid-game without a proper
`GameOver` transition.

**`BonusFreezeSeconds` equals zero**: The freeze phase completes instantly on the same
frame. `IsFrozen` is set and unset within the same `Update()` call. This is a valid
degenerate config (no bonus), not an error.

**`StartDuration` of zero**: Timer begins in `Expired` state immediately on `StartTimer()`.
This is valid for testing purposes and should be handled without a null-frame crash.

**Frame spike**: If `Time.deltaTime` is abnormally large (e.g., first frame after a
stutter, `deltaTime = 2.0s`) and `TimeRemaining = 0.5s`, the clamp to zero prevents
`TimeRemaining` from going negative. `OnTimerExpired` fires once.

**`MaxTime` ceiling during bonus**: If `BonusFreezeSeconds` is configured as 0 and an
additive bonus variant is ever introduced, the formula `min(MaxTime, TimeRemaining + bonus)`
must be applied. The current freeze-only design makes this a non-issue, but the ceiling
must be enforced by any future bonus type.

**Timer mode not active**: The Timer System should still be instantiated in turn-limit
sessions but configured with `StartDuration = 0` and never started. This avoids
conditional null checks in systems that subscribe to timer events.

**Degenerate strategy — spamming short words for freeze chains**: A player who submits
many short valid words rapidly could maintain a near-permanent freeze state. Mitigation:
the freeze resets (not stacks), and the brief un-frozen window between submissions
still drains time. No hard counter needed; this is skilled play, not an exploit.

---

## Dependencies

### This system requires from others

| System | What it needs |
|--------|--------------|
| Board Manager | `OnSubmissionAttempted(result: SubmissionResult)` event, where `SubmissionResult` includes at minimum a boolean `IsValid` flag |
| Game State Machine | Calls `Configure(TimerConfig)`, `StartTimer()`, `Pause()`, `Resume()` in correct lifecycle order |
| Game Mode Manager | Provides the `TimerConfig` data object before `StartTimer()` is called |

### This system provides to others

| System | What it provides |
|--------|-----------------|
| Turn/Timer HUD | `OnTick(float timeRemaining, bool isFrozen)` — fired every frame when Running |
| Turn/Timer HUD | `OnLowTime(float remaining)` — fired once when crossing threshold |
| Game State Machine | `OnTimerExpired` — fired once when time hits zero |
| Pause Menu | `Pause()` and `Resume()` methods |
| Any subscriber | `IsFrozen` (bool property), `TimeRemaining` (float property), `State` (enum property) |

---

## Tuning Knobs

All values live in `assets/data/timer-config.json` (or a Unity ScriptableObject at
`assets/data/TimerModeConfig.asset`). Never hardcoded.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `StartDuration` | Gate | 120.0s | 30 – 300s | Total session length in timer mode. Lower = higher intensity. |
| `BonusFreezeSeconds` | Feel | 3.0s | 0 – 8s | How long the timer pauses on a valid word. Too high = trivially easy; too low = reward feels meaningless. |
| `MaxTime` | Gate | 120.0s | Equal to or greater than `StartDuration` | Ceiling on `TimeRemaining` — prevents bonus time from exceeding the starting duration. Currently equal to `StartDuration` (no accumulation above start). |
| `LowTimeThreshold` | Feel | 10.0s | 5 – 20s | When the HUD urgency pulse begins. Lower = more intense late-game; higher = longer anxiety window. |

**Rationale for defaults:**
- 120 seconds targets a ~2-minute session matching the turn-limit mode's expected session
  length (approximately 20 turns at ~6 seconds per turn decision).
- 3-second freeze was chosen over an additive bonus (+N seconds) because it is
  visually clearer (the countdown visibly stops), avoids time accumulation above the
  starting cap, and creates a consistent "breath" regardless of remaining time.
- 10-second threshold gives the HUD approximately 2-3 final word submissions worth of
  warning time, which matches the desired adrenaline spike window.

---

## Acceptance Criteria

### Functional

- [ ] Timer starts at `StartDuration` when `StartTimer()` is called.
- [ ] Timer counts down at real-time speed (verified: 10s wall clock = 10s elapsed, within ±0.05s across 3 trials).
- [ ] `OnTimerExpired` fires exactly once per session, on the frame `TimeRemaining` reaches zero.
- [ ] `OnTimerExpired` does not fire twice when session ends normally.
- [ ] Valid word submission triggers `IsFrozen = true` for exactly `BonusFreezeSeconds` (±1 frame).
- [ ] `TimeRemaining` does not decrease while `IsFrozen == true`.
- [ ] `TimeRemaining` does not decrease while in `Paused` state.
- [ ] `OnLowTime` fires exactly once per session when `TimeRemaining` first crosses `LowTimeThreshold`.
- [ ] `OnLowTime` does not re-fire on subsequent frames or after a freeze-thaw cycle.
- [ ] `Pause()` during an active freeze: both freeze and main countdown pause together.
- [ ] `Resume()` after such a pause: both resume from their respective paused values.
- [ ] Second valid word during freeze: `FreezeTimeRemaining` resets to `BonusFreezeSeconds` (not stacked).
- [ ] Invalid word submission during any Running sub-state: no change to timer.

### Experiential (Playtest Validation)

- [ ] Playtesters report the freeze feels like a "reward" and not confusing or abrupt.
- [ ] Playtesters can read `TimeRemaining` at a glance without counting (HUD integration check).
- [ ] The low-time warning causes a noticeable shift in player behavior (faster decisions, shorter words) in at least 3 of 5 playtest sessions.
- [ ] No playtester reports feeling the timer is "unfair" due to frame-rate inconsistency.

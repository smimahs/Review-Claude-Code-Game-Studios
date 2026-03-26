# Turn Manager

> Status: Designed | Author: Claude Code Game Studios | Last Updated: 2026-03-26

---

## Overview

The Turn Manager owns the turn budget for turn-limit game mode. It holds a single counter, `_turnsRemaining`, initialized from an external config value at game start. It subscribes to `BoardManager.OnSubmissionAttempted` and decrements the counter on every submission event — valid or invalid — because the design intent is that committing to a word attempt always costs something. After each decrement it fires `OnTurnsChanged` with the new count so HUD and other UI can update. When `_turnsRemaining` reaches zero it fires `OnTurnsExhausted`, which the Game Mode Manager listens to in order to end the game. The Turn Manager never ends the game itself; it only reports count state.

---

## Player Fantasy

The player should feel the weight of each decision. Every reel selection and submission is a meaningful expenditure from a finite budget, not a free experiment. The dwindling turn count visible in the HUD should create a rising pressure that makes each word choice feel more consequential as the game progresses — a satisfying tension between wanting to find high-value words and needing to commit before the budget runs out.

MDA Aesthetics primarily served: **Challenge** (finite-resource decision making under pressure) and **Submission** (the satisfying finality of a budget running out that triggers score review).

---

## Detailed Design

### Core Rules

1. `_turnsRemaining` is an integer initialized to `InitialTurns` at the start of each game session. It is never modified before the Playing state is entered.

2. Turn Manager subscribes to `BoardManager.OnSubmissionAttempted(string word, bool isValid)` at initialization. It does not distinguish between valid and invalid submissions — both cost exactly one turn.

3. On receiving `OnSubmissionAttempted`, Turn Manager:
   - Decrements `_turnsRemaining` by 1.
   - Fires `OnTurnsChanged(int remaining)` with the new value.
   - If `_turnsRemaining` is now 0, fires `OnTurnsExhausted()`.

4. `OnTurnsExhausted` is fired exactly once per game session. After firing, Turn Manager stops responding to `OnSubmissionAttempted` events (it unsubscribes or guards with an `_isExhausted` flag) to prevent double-firing if a race condition occurs.

5. Turn Manager does not cancel or interrupt the current submission. `OnTurnsExhausted` fires after the full `OnSubmissionAttempted` event chain completes (i.e. after Turn Manager's handler returns, not mid-pipeline). The Board Manager's submission pipeline is already complete before `OnTurnsExhausted` propagates to Game Mode Manager.

6. Turn Manager does not know about scoring, reel state, or word validity. It has a single responsibility: counting submissions and reporting count state.

7. Turn Manager exposes a read-only `TurnsRemaining` property for UI polling (though the preferred pattern is subscribing to `OnTurnsChanged`).

### States and Transitions

Turn Manager is stateless in the FSM sense. It has one lifecycle flag:

| Flag | Value | Meaning |
|------|-------|---------|
| `_isExhausted` | false | Normal operation; responding to submissions |
| `_isExhausted` | true | Turns exhausted; ignoring further submission events |

The flag is set to `false` on `Initialize()` and to `true` immediately before firing `OnTurnsExhausted()`.

### Interactions with Other Systems

**Board Manager (listens to):** Turn Manager subscribes to `BoardManager.OnSubmissionAttempted`. This is its only input.

**Turn/Timer HUD (fires event to):** Turn/Timer HUD subscribes to `OnTurnsChanged` to update the displayed turn count.

**Game Mode Manager (fires event to):** Game Mode Manager subscribes to `OnTurnsExhausted` to trigger the game-over flow.

---

## Formulas

### Turns Remaining

```
TurnsRemaining(n) = InitialTurns - SubmissionCount(n)

  where:
    InitialTurns      = starting turn budget (config value, default 20)
    SubmissionCount(n) = total number of OnSubmissionAttempted events received
                         since session start, inclusive of submission n
    TurnsRemaining(n) ∈ [0, InitialTurns]
```

Example with InitialTurns = 20:
- After 1 submission: TurnsRemaining = 19
- After 15 submissions: TurnsRemaining = 5
- After 20 submissions: TurnsRemaining = 0 → OnTurnsExhausted fires

### Turn Pressure Curve

Turn pressure is not directly modeled as a formula; it emerges from the ratio of remaining turns to initial turns. For UI and audio cueing purposes, the following threshold bands are defined in config:

```
PressureLevel = TurnsRemaining / InitialTurns

  PressureLevel > 0.5  → Normal (no warning)
  0.25 < PressureLevel ≤ 0.5 → Caution (optional mild warning cue)
  0 < PressureLevel ≤ 0.25 → Danger (strong warning cue)
  PressureLevel = 0    → Exhausted
```

These thresholds are tuning knobs consumed by the Turn/Timer HUD and Audio Manager. Turn Manager itself only reports the integer count.

---

## Edge Cases

**`_turnsRemaining` reaches 0 on a valid word submission:** The valid word is processed completely (reel advancement, score increment, `OnWordSubmitted` fires) before Turn Manager fires `OnTurnsExhausted`. The player receives credit for the final word. This is the correct behavior — the turn was spent on a successful play.

**`_turnsRemaining` reaches 0 on an invalid submission:** Same flow. `OnSubmissionAttempted(word, false)` fires first (Turn Manager decrements, fires `OnTurnsExhausted`), then Board Manager completes its pipeline (no reel advancement, no score). The player loses their last turn on a failed attempt. This is intentional and clearly communicated by the design pillar of strategic clarity.

**`OnSubmissionAttempted` received after `_isExhausted = true`:** Turn Manager's handler checks `_isExhausted` first and returns immediately. This prevents `_turnsRemaining` from going negative. Log a warning (this would indicate an event subscription cleanup issue).

**`InitialTurns` configured to 0:** Illegal configuration. Turn Manager should validate at initialization and throw a configuration exception if `InitialTurns < 1`. Default to 20 as fallback with error log.

**`InitialTurns` configured to a very large number (e.g. 9999):** Functionally correct but ruins the tension mechanic. The safe range cap of 100 in config should prevent this in shipped builds. Documented as designer-responsibility.

**Session reset / restart:** Turn Manager must expose a `Reset()` method that sets `_turnsRemaining = InitialTurns` and `_isExhausted = false`. This is called by Game Mode Manager when a new game session begins. Failing to reset causes the second game to start with 0 turns.

**Pause state:** Turn Manager does not pause. It is purely event-driven (reacts to submission events). Since Board Manager blocks all input during Paused state, no `OnSubmissionAttempted` events will fire while paused. No special handling needed in Turn Manager.

---

## Dependencies

### Provided to Other Systems (Outbound Events)

| Event | Signature | Consumer(s) |
|-------|-----------|-------------|
| `OnTurnsChanged` | `(int turnsRemaining)` | Turn/Timer HUD |
| `OnTurnsExhausted` | `()` | Game Mode Manager |

### Required from Other Systems (Inbound Events)

| System | Event Subscribed | Usage |
|--------|-----------------|-------|
| Board Manager | `OnSubmissionAttempted(string word, bool isValid)` | Triggers turn decrement |

### Lifecycle Dependency

Turn Manager must subscribe to `BoardManager.OnSubmissionAttempted` after Board Manager is initialized and before the Playing state is entered. Turn Manager must call `Reset()` before each new game session.

---

## Tuning Knobs

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `InitialTurns` | Gate | 20 | 5–100 | Total turn budget per session. Lower values increase pressure and shorten sessions. Higher values allow exploration but reduce tension. 20 targets approximately 5–10 minutes of play per session based on average 15–30 seconds per turn. |
| `CautionThreshold` | Feel | 0.5 | 0.1–0.8 | Fraction of turns remaining below which caution UI/audio cues activate. Consumed by HUD and Audio Manager. |
| `DangerThreshold` | Feel | 0.25 | 0.05–0.5 | Fraction of turns remaining below which danger cues activate. Must be less than `CautionThreshold`. |

All tuning knobs are defined in `assets/data/turn-config.json`.

---

## Acceptance Criteria

### Functional Criteria

- **FC-TM-01**: At game start with `InitialTurns = 20`, `TurnsRemaining = 20`.
- **FC-TM-02**: After 1 valid word submission, `TurnsRemaining = 19` and `OnTurnsChanged(19)` has fired exactly once.
- **FC-TM-03**: After 1 invalid word submission, `TurnsRemaining = 19` and `OnTurnsChanged(19)` has fired exactly once. (Invalid submissions cost turns.)
- **FC-TM-04**: After exactly 20 submissions (any mix of valid/invalid), `TurnsRemaining = 0` and `OnTurnsExhausted` has fired exactly once.
- **FC-TM-05**: `OnTurnsExhausted` does not fire before `TurnsRemaining` reaches 0.
- **FC-TM-06**: `OnTurnsExhausted` fires at most once per session regardless of additional submissions received.
- **FC-TM-07**: Calling `Reset()` with `InitialTurns = 20` restores `TurnsRemaining = 20` and clears `_isExhausted`.
- **FC-TM-08**: `OnTurnsChanged` fires on every decrement, including the final decrement that brings the count to 0.
- **FC-TM-09**: `TurnsRemaining` never goes below 0.

### Experiential Criteria (Playtest Validation)

- **EC-TM-01**: Players feel urgency increasing as turns deplete. (Validated by player self-report in post-session interview: "did you feel increasing pressure as turns ran out?")
- **EC-TM-02**: Players understand that invalid submissions also cost turns. (Validated by observing whether players attempt fewer invalid submissions after losing a turn to one.)
- **EC-TM-03**: The turn count displayed in the HUD matches the internal `TurnsRemaining` value at all times. (Validated by QA automated test comparing event payloads to rendered HUD text.)

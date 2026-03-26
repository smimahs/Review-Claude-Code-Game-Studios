# Game Mode Manager

> Status: Designed | Author: Claude Code Game Studios | Last Updated: 2026-03-26

---

## Overview

The Game Mode Manager selects and configures the active game mode (turn-limit or timer) before each session, wires the appropriate end-condition listener, and fires `OnGameOver(GameResult)` when the game ends. It acts as the bridge between the mode-specific end-condition systems (Turn Manager for turn-limit mode, Timer System for timer mode) and the top-level Game State Machine. For MVP, only turn-limit mode is implemented and active; timer mode is architecturally planned but not wired. The Game Mode Manager also constructs the `GameResult` data object, aggregating final score, words found count, and turns used from the relevant sources.

---

## Player Fantasy

From the player's perspective this system is invisible — it is pure infrastructure. Its contribution to player experience is that the game always ends cleanly and correctly with a well-formed result. A poorly designed mode manager produces bugs like the game ending early, not ending at all, or ending twice, all of which break immersion. The Game Mode Manager's job is to make the ending feel inevitable and earned: the last turn is spent, the game acknowledges it, and the final score is presented without hesitation.

MDA Aesthetics served: **Submission** (the satisfying resolution of a complete game arc).

---

## Detailed Design

### Core Rules

1. Game Mode Manager holds a `_activeMode` enum value: `TurnLimit` or `Timer`. For MVP, this is always `TurnLimit`.

2. **Mode configuration** happens once, during the transition from MainMenu to Playing state. The Game State Machine fires `OnStateChanged(MainMenu, Playing)`, which triggers Game Mode Manager to call `Configure(_activeMode)`.

3. **`Configure(GameMode mode)` execution:**
   - Unsubscribes from any previously wired end-condition events (safety for restart).
   - Calls `Reset()` on the relevant subsystems (Turn Manager for `TurnLimit`; Timer System for `Timer`).
   - Subscribes to the appropriate end-condition event:
     - `TurnLimit`: subscribes to `TurnManager.OnTurnsExhausted`
     - `Timer`: subscribes to `TimerSystem.OnTimerExpired` (post-MVP)
   - Resets the Game Mode Manager's internal `_isGameOver` guard flag to `false`.
   - Resets the internal words-found counter to 0.
   - Subscribes to `BoardManager.OnWordSubmitted` to count valid words.

4. **End-condition handler** (`HandleTurnsExhausted` or `HandleTimerExpired`):
   - If `_isGameOver` is already `true`, return immediately (prevents double-firing).
   - Set `_isGameOver = true`.
   - Collect `FinalScore` from `ScoringSystem.SessionTotal`.
   - Collect `WordsFound` from internal counter `_wordsFoundCount`.
   - Collect `TurnsUsed` from `InitialTurns - TurnManager.TurnsRemaining` (turn-limit mode) or `TimerSystem.ElapsedTime` (timer mode).
   - Construct `GameResult` struct.
   - Fire `OnGameOver(GameResult result)`.

5. **`GameResult` struct definition:**

   ```
   GameResult {
     int   FinalScore     // total session score from ScoringSystem.SessionTotal
     int   WordsFound     // count of valid word submissions this session
     int   TurnsUsed      // submissions made = InitialTurns - TurnsRemaining (turn-limit)
                          // OR elapsed seconds (timer mode, post-MVP)
     GameMode  ModeUsed   // TurnLimit or Timer
   }
   ```

6. Game Mode Manager does not track score internally. It reads it from ScoringSystem at game-over time only.

7. Game Mode Manager subscribes to `BoardManager.OnWordSubmitted` (not `OnSubmissionAttempted`) for counting valid words, since invalid submissions should not increment `_wordsFoundCount`.

### States and Transitions

Game Mode Manager has two internal states:

| State | Meaning |
|-------|---------|
| `Configured` | Mode is set, end-condition listener is active, session is running |
| `GameOver` | `OnGameOver` has been fired; no further end-condition responses |

The transition `Configured → GameOver` is triggered by the end-condition event. There is no transition back to `Configured` from `GameOver`; instead, a new call to `Configure()` resets the system entirely.

### Interactions with Other Systems

**Game State Machine (listens to / fires to):** Listens to `OnStateChanged(GameState prev, GameState next)` to detect when Playing begins. Fires `OnGameOver(GameResult)` which the Game State Machine listens to for triggering the `Playing → GameOver` transition.

**Turn Manager (listens to):** Subscribes to `OnTurnsExhausted` in turn-limit mode.

**Timer System (listens to, post-MVP):** Subscribes to `OnTimerExpired` in timer mode.

**Board Manager (listens to):** Subscribes to `OnWordSubmitted` to count valid words.

**Scoring System (reads from):** Reads `ScoringSystem.SessionTotal` at game-over time to populate `GameResult.FinalScore`.

---

## Formulas

### TurnsUsed Calculation (Turn-Limit Mode)

```
TurnsUsed = InitialTurns - TurnManager.TurnsRemaining

  where:
    InitialTurns         = configured starting budget (from turn-config.json)
    TurnManager.TurnsRemaining = remaining count at game-over moment
    TurnsUsed ∈ [1, InitialTurns]
    (minimum 1 because at least 1 submission occurred to exhaust turns)
```

Example: InitialTurns = 20, TurnsRemaining = 0 → TurnsUsed = 20.
Example: InitialTurns = 20, TurnsRemaining = 5 → TurnsUsed = 15 (hypothetical early-end scenario).

### Words Found

```
WordsFound = count of OnWordSubmitted events received since Configure() was called

  WordsFound ∈ [0, TurnsUsed]
  (cannot find more valid words than turns spent)
```

### Validity Rate (informational, not stored in GameResult at MVP)

```
ValidityRate = WordsFound / TurnsUsed
  range: [0.0, 1.0]
```

This metric is not exposed at MVP but is useful for future analytics and difficulty calibration.

---

## Edge Cases

**`OnTurnsExhausted` fires before `Configure()` is called:** Impossible in correct initialization order (Turn Manager is reset inside `Configure()`). If it somehow occurs (bad initialization order), the `_isGameOver` guard is already `false` but `_activeMode` may be unset. Game Mode Manager should guard with an `_isConfigured` flag and log an error if an end-condition event fires before configuration.

**`OnGameOver` fires twice:** Prevented by `_isGameOver` guard flag. The second call to the end-condition handler returns immediately. This protects against any edge case where both `OnTurnsExhausted` and a direct call path fire simultaneously.

**Player restarts mid-session:** `Configure()` is called again at the start of the new session. It unsubscribes all previous event listeners, resets all counters, and re-subscribes cleanly. No state leaks from the previous session.

**`ScoringSystem.SessionTotal` is 0 at game-over:** Legal. The player may have submitted only invalid words. `GameResult.FinalScore = 0` is valid. No special handling.

**`WordsFound` is 0 at game-over:** Legal. `GameResult.WordsFound = 0`. Represents a session where the player used all turns on invalid submissions.

**Timer mode `OnTimerExpired` fires while a submission is in progress (post-MVP concern):** The `_isGameOver` flag prevents double-invocation. The in-progress submission should be allowed to complete (Board Manager is in `Submitting` state). The Game Mode Manager's `OnGameOver` fires after the event handler returns, and the Game State Machine handles the transition at its next opportunity. This ordering should be documented in the Timer System GDD.

**Mode selection UI not implemented (MVP):** For MVP, `_activeMode` is hardcoded to `TurnLimit` in the inspector or config file. No mode selection UI is required. This is explicitly a post-MVP feature.

---

## Dependencies

### Provided to Other Systems (Outbound Events)

| Event | Signature | Consumer(s) |
|-------|-----------|-------------|
| `OnGameOver` | `(GameResult result)` | Game State Machine |

### Required from Other Systems (Inbound Events / Calls)

| System | Dependency Type | Usage |
|--------|----------------|-------|
| Game State Machine | Event subscription: `OnStateChanged` | Detects `MainMenu → Playing` to trigger `Configure()` |
| Turn Manager | Event subscription: `OnTurnsExhausted` | End condition for turn-limit mode |
| Timer System | Event subscription: `OnTimerExpired` (post-MVP) | End condition for timer mode |
| Board Manager | Event subscription: `OnWordSubmitted` | Counts valid words for GameResult |
| Scoring System | Property read: `SessionTotal` (int) | Populates GameResult.FinalScore at game-over |

### Lifecycle Dependency

Game Mode Manager must initialize after Game State Machine, Turn Manager, and Board Manager. It must subscribe to Game State Machine's `OnStateChanged` during its own initialization so it can detect when Playing begins.

---

## Tuning Knobs

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `DefaultGameMode` | Gate | `TurnLimit` | `TurnLimit` / `Timer` | Which mode is active at MVP. Post-MVP, this becomes a player-selected option. |

All configuration is defined in `assets/data/game-mode-config.json`.

Note: The specific tuning knobs for each mode (initial turns, timer duration) live in the respective subsystem config files (`turn-config.json`, `timer-config.json`). Game Mode Manager does not own those values.

---

## Acceptance Criteria

### Functional Criteria

- **FC-GMM-01**: After `Configure(TurnLimit)` is called and 20 submissions occur, `OnGameOver` fires exactly once with a non-null `GameResult`.
- **FC-GMM-02**: `GameResult.FinalScore` equals `ScoringSystem.SessionTotal` at the moment `OnGameOver` fires.
- **FC-GMM-03**: `GameResult.WordsFound` equals the count of `OnWordSubmitted` events received since `Configure()` was called.
- **FC-GMM-04**: `GameResult.TurnsUsed` equals `InitialTurns - TurnManager.TurnsRemaining` at the moment `OnGameOver` fires.
- **FC-GMM-05**: `OnGameOver` does not fire more than once per session regardless of how many end-condition events are received.
- **FC-GMM-06**: Calling `Configure()` a second time (restart scenario) resets `_wordsFoundCount` to 0 and `_isGameOver` to false.
- **FC-GMM-07**: `GameResult.ModeUsed` equals `TurnLimit` when configured in turn-limit mode.
- **FC-GMM-08**: Game Mode Manager does not call `TurnManager.Reset()` at any time other than inside `Configure()`.

### Experiential Criteria (Playtest Validation)

- **EC-GMM-01**: The game always ends at the correct moment — exactly when the last turn is spent, with no additional delay and no premature termination. (Validated by QA test: 20 submissions in a session results in game-over screen appearing.)
- **EC-GMM-02**: The final score shown on the Game Over Screen matches the score accumulated during play. (Validated by QA test comparing running score total to `GameResult.FinalScore`.)

# Game State Machine

> Status: Designed | Author: Claude Code Game Studios | Last Updated: 2026-03-26

---

## Overview

The Game State Machine is the top-level coordinator of ReelWords' application lifecycle. It manages four mutually exclusive states — `MainMenu`, `Playing`, `Paused`, and `GameOver` — and enforces all valid transitions between them. On every transition it fires `OnStateChanged(GameState previous, GameState next)`, which Scene Flow and all UI systems listen to in order to load scenes, show or hide overlays, and configure themselves. The Game State Machine owns no gameplay logic; it is purely an orchestrator of application state. Invalid transition attempts are rejected and logged rather than silently ignored or allowed, ensuring that no subsystem can accidentally push the application into an incoherent state.

---

## Player Fantasy

Like the Game Mode Manager, this system is completely invisible to the player when working correctly. Its contribution is smoothness and reliability: the game always arrives at the right screen at the right time, overlays appear and disappear without glitching, and pressing Pause during a game always pauses exactly once. Players notice state machine failures acutely (double game-over screens, inability to return to menu, pause not working) but never notice correct operation. The goal is a system so stable it is forgettable.

MDA Aesthetics served: **Submission** (the rhythm of menu → play → result feels clean and complete).

---

## Detailed Design

### Core Rules

1. The Game State Machine holds a single `_currentState` variable of type `GameState` enum. Valid enum values: `MainMenu`, `Playing`, `Paused`, `GameOver`.

2. The initial state at application launch is `MainMenu`. This is set before any other system initializes.

3. All state transitions are triggered by calling the public method `RequestTransition(GameState targetState)`. No system other than the Game State Machine may write to `_currentState`.

4. **Transition validation:** Before executing a transition, the state machine checks the transition table. If the requested transition is not in the valid transition table (see below), the request is rejected: `_currentState` does not change, no events fire, and a warning is logged with the format: `[GameStateMachine] Invalid transition rejected: {current} → {target}`.

5. **Transition execution (when valid):**
   - Store `previousState = _currentState`.
   - Set `_currentState = targetState`.
   - Fire `OnStateChanged(GameState previous, GameState next)`.

6. The state machine does not subscribe to game events from other systems directly in the base design. Instead, other systems call `RequestTransition()` on it. Exception: **Game Mode Manager** calls `RequestTransition(GameOver)` in response to its own `OnGameOver` event — this is the only event-driven transition. All other transitions are player-input-driven.

7. **Re-entrant transition guard:** If `RequestTransition()` is called while a transition is already executing (i.e., from within an `OnStateChanged` listener), the nested call is deferred to the next frame via a queued request. At most one deferred request may be queued at a time. If a second nested transition arrives before the first deferred one is processed, it replaces the deferred request and a warning is logged.

### Valid Transition Table

| From | To | Trigger | Who Calls RequestTransition |
|------|----|---------|----------------------------|
| `MainMenu` | `Playing` | Player presses Start button | Main Menu UI |
| `Playing` | `Paused` | Player presses Pause button | Pause button handler |
| `Paused` | `Playing` | Player presses Resume button | Pause Menu UI |
| `Playing` | `GameOver` | Turns exhausted or timer expired | Game Mode Manager |
| `GameOver` | `MainMenu` | Player presses Main Menu button | Game Over Screen UI |

All other transition pairings are invalid. Specifically:

| Attempted | Outcome |
|-----------|---------|
| `MainMenu → Paused` | Rejected (cannot pause at main menu) |
| `MainMenu → GameOver` | Rejected |
| `Paused → GameOver` | Rejected (game cannot end while paused) |
| `Paused → MainMenu` | Rejected (must resume before quitting, or use a dedicated quit flow — post-MVP) |
| `GameOver → Playing` | Rejected (must return to menu first, then start new game) |
| `GameOver → Paused` | Rejected |
| `Playing → MainMenu` | Rejected (must go through GameOver or implement a quit flow) |
| Any state → same state | Rejected (no self-transitions) |

**MVP Note on `Paused → MainMenu`:** A "Quit to Main Menu" button from the Pause Menu is a desirable feature but is post-MVP (Pause Menu is post-MVP per the systems index). For MVP, the only way to reach MainMenu from Playing is through GameOver. This is documented here so the transition can be added to the valid table when Pause Menu is implemented.

### States and Transitions

```
              [Start button pressed]
                        |
     ┌──────────────────▼──────────────────┐
     │            MainMenu                 │
     └──────────────────┬──────────────────┘
                        │ RequestTransition(Playing)
                        ▼
     ┌──────────────────────────────────────┐
     │              Playing                 │◄──[Resume]──┐
     └──┬───────────────────────────────────┘             │
        │                          │                      │
        │ [Pause]                  │ [Turns exhausted /   │
        ▼                          │  Timer expired]      │
     ┌─────────┐                   ▼                      │
     │ Paused  │──────────►  ┌──────────┐                 │
     └─────────┘             │ GameOver │                 │
                             └────┬─────┘                 │
                                  │ [Main Menu button]    │
                                  ▼                       │
                             ┌──────────┐                 │
                             │ MainMenu │─────[Start]─────┘
                             └──────────┘
```

### Interactions with Other Systems

**Scene Flow (fires events to):** Scene Flow subscribes to `OnStateChanged` and loads/unloads scenes in response. Board Manager fires no events to Scene Flow directly.

**Main Menu UI (receives calls from / fires calls to):** Main Menu listens to `OnStateChanged` to know when to show itself. Its Start button calls `RequestTransition(Playing)`.

**Game Over Screen UI (receives calls from / fires calls to):** Listens to `OnStateChanged` to know when to show the overlay. Its Main Menu button calls `RequestTransition(MainMenu)`.

**Pause Menu UI (receives calls from / fires calls to, post-MVP):** Listens to `OnStateChanged` to show/hide. Its Resume button calls `RequestTransition(Playing)`.

**Game Mode Manager (fires calls to):** Subscribes to its own `OnGameOver` and then calls `GameStateMachine.RequestTransition(GameOver)`.

**Board Manager (implicit coordination):** Board Manager should not accept input while in any state other than `Playing`. Board Manager subscribes to `OnStateChanged` and enables/disables input accordingly. Board Manager does not call `RequestTransition` itself.

---

## Formulas

The Game State Machine contains no numeric formulas. It is a finite automaton over a discrete state space. The formal definition:

```
States S = { MainMenu, Playing, Paused, GameOver }
Initial state s₀ = MainMenu
Transition relation δ ⊆ S × S (as defined in the valid transition table above)
Output: OnStateChanged(sᵢ, sⱼ) fires on every valid transition (sᵢ, sⱼ) ∈ δ
```

---

## Edge Cases

**`RequestTransition` called with current state as target (self-transition):** Treated as an invalid transition. Rejected and logged. No event fires. This prevents listeners from reacting to a no-op transition and potentially reinitializing themselves.

**`RequestTransition(GameOver)` called from multiple sources simultaneously:** The `_isTransitioning` guard prevents re-entrant transitions. The first call succeeds; any simultaneous call is deferred. If the deferred call is also `GameOver` (already the current state), it is an invalid self-transition and is silently discarded after the queue is processed.

**Application focus lost (alt-tab) while in Playing state:** This is an OS/engine event, not a state machine concern. The game should auto-pause when focus is lost (post-MVP). For MVP, no auto-pause is required. The state machine does not respond to OS focus events.

**Game Over Screen calls `RequestTransition(Playing)` directly (programmer error):** This transition is not in the valid table (`GameOver → Playing` is rejected). The transition is blocked, a warning is logged, and the game remains on the Game Over screen. The player sees no change; a bug report is generated.

**Scene loading fails during a transition:** Scene Flow is responsible for handling scene load failures. The Game State Machine fires `OnStateChanged` and considers its job done. If Scene Flow encounters an error, it should fire its own error event. The Game State Machine does not roll back the state transition — the state is already `Playing` (or `MainMenu`) even if the scene failed to load. This is an edge case for Scene Flow to handle.

**`RequestTransition` called before `_currentState` is initialized:** The initial state is set in `Awake()` before any other `Start()` calls. Unity's execution order should prevent this. If it occurs (e.g. due to execution order misconfiguration), the transition table lookup finds no valid entry from an undefined state and rejects the request. An error is logged.

**Degenerate strategy — spamming Start button:** If the player rapidly presses Start on the Main Menu, multiple `RequestTransition(Playing)` calls arrive. The first succeeds and triggers scene loading. Subsequent calls are rejected (the state is already `Playing`). Scene loading is idempotent — only one load occurs.

---

## Dependencies

### Provided to Other Systems (Outbound Events)

| Event | Signature | Consumer(s) |
|-------|-----------|-------------|
| `OnStateChanged` | `(GameState previous, GameState next)` | Scene Flow, Main Menu UI, Game Over Screen UI, Pause Menu UI (post-MVP), Board Manager, Game Mode Manager, Turn Manager (for reset), Board UI |

### Provided to Other Systems (Public API)

| Method / Property | Signature | Consumer(s) |
|-------------------|-----------|-------------|
| `RequestTransition` | `(GameState targetState): void` | Main Menu UI, Pause button handler, Pause Menu UI (post-MVP), Game Mode Manager |
| `CurrentState` | `GameState` (read-only property) | Any system needing to query current state |

### Required from Other Systems

The Game State Machine has no event subscriptions. All transitions are initiated by external callers via `RequestTransition()`. It is a pure outbound-event, inbound-call system.

### Lifecycle Dependency

Game State Machine must initialize first among all systems. It sets `_currentState = MainMenu` in `Awake()`. All other systems that subscribe to `OnStateChanged` must do so in their own `Awake()` or `Start()` calls, which by Unity execution order will occur after the Game State Machine's `Awake()`.

---

## Tuning Knobs

The Game State Machine has no numeric tuning knobs. Its behavior is entirely determined by the valid transition table, which is a design constant not intended for runtime tuning.

The only configurable aspect is the **deferred transition queue size**, but this is an implementation detail:

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `MaxDeferredTransitions` | Feel | 1 | 1–3 | Maximum number of nested transition requests queued while a transition is in progress. Values above 1 may cause unexpected state chains. |

Defined in `assets/data/game-state-config.json`.

---

## Acceptance Criteria

### Functional Criteria

- **FC-GSM-01**: At application launch, `CurrentState == MainMenu`.
- **FC-GSM-02**: `RequestTransition(Playing)` from `MainMenu` succeeds: `CurrentState` becomes `Playing` and `OnStateChanged(MainMenu, Playing)` fires exactly once.
- **FC-GSM-03**: `RequestTransition(Paused)` from `Playing` succeeds. `RequestTransition(Playing)` from `Paused` succeeds.
- **FC-GSM-04**: `RequestTransition(GameOver)` from `Playing` succeeds (simulating a Game Mode Manager call).
- **FC-GSM-05**: `RequestTransition(MainMenu)` from `GameOver` succeeds.
- **FC-GSM-06**: Every invalid transition from the rejection table is tested: each call is rejected (no state change, no `OnStateChanged` event, warning logged).
- **FC-GSM-07**: `RequestTransition(Playing)` from `Playing` (self-transition) is rejected with no event and a warning.
- **FC-GSM-08**: Rapid consecutive `RequestTransition(Playing)` calls from `MainMenu` result in exactly one `OnStateChanged` event and one successful transition.
- **FC-GSM-09**: `OnStateChanged` always carries the correct `previous` and `next` state values matching the transition that occurred.
- **FC-GSM-10**: `CurrentState` is never in an undefined state (i.e. is always one of the four valid enum values).

### Experiential Criteria (Playtest Validation)

- **EC-GSM-01**: Pressing Start, Pause, Resume, and navigating to Main Menu from Game Over all respond within one frame and produce no visible glitches. (Validated by frame-capture review of scene transitions.)
- **EC-GSM-02**: It is impossible through any input sequence to reach a state where the game is visually "stuck" with no way to proceed. (Validated by exploratory QA stress testing the full transition graph.)

# Turn/Timer HUD

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic Clarity — board state always legible

---

## Overview

The Turn/Timer HUD is a single adaptive HUD component that displays the
remaining resource for whichever game mode is active. In turn-limit mode it
shows a "Turns: N" counter with an optional progress bar that depletes as
turns are consumed; in timer mode it shows a "MM:SS" countdown. The component
resolves which display mode to use at initialization from the Game Mode Manager
and then responds to events from either Turn Manager or Timer System throughout
the session. A low-resource warning (color change to red/orange) triggers when
the remaining resource falls below a configurable threshold. The component is
implemented entirely in Unity 6 UI Toolkit (UIDocument, UXML, USS); no UGUI
components are used.

---

## Player Fantasy

The HUD should create controlled pressure without panic. In turn-limit mode the
player feels like a chess player counting their remaining moves — deliberate,
strategic, aware that time is finite but not rushed. The number ticking down
creates a soft ticking-clock psychology even without a literal timer. In timer
mode the MM:SS countdown creates genuine urgency; as the seconds drain, the
player should feel the satisfying squeeze of needing to think faster. The
red/orange low-resource warning should feel like a siren going off — not
punishing, but electrifying.

The primary MDA aesthetics this mechanic serves:
- **Challenge**: the diminishing resource directly communicates the Competence
  pressure that is central to Flow state design. The player must feel that
  the game has stakes.
- **Sensation**: the color shift on low resource creates a visceral urgency
  pulse at the moment when stakes are highest.

---

## Detailed Design

### Core Rules

**Mode Resolution at Initialization**
On `Awake` or `OnEnable`, the HUD queries `GameModeManager.ActiveMode` to
determine display mode:
- `GameMode.TurnLimit` → enter Turn Display Mode
- `GameMode.Timer` → enter Timer Display Mode

The HUD does not switch mode mid-session. Mode is fixed for the duration of
one game session and re-evaluated fresh on the next `OnGameStarted`.

**Turn Display Mode**

Layout elements active in this mode:
- `turns-label`: text element showing `"Turns: N"` where N is the integer
  remaining turn count. Examples: `"Turns: 15"`, `"Turns: 3"`, `"Turns: 0"`.
- `turns-progress-bar` (optional; see Tuning Knobs `SHOW_TURNS_PROGRESS_BAR`):
  a horizontal bar whose fill ratio equals `turnsRemaining / turnsMax`.

Data source: `TurnManager.TurnsRemaining` (int), `TurnManager.TurnsMax` (int).

Events subscribed:
- `TurnManager.OnTurnConsumed(int turnsRemaining)`: update the label and bar.
- `TurnManager.OnGameOver()`: update to show `"Turns: 0"` and apply the
  `hud--depleted` USS class (full red/orange state).

Low-turn warning threshold: when `turnsRemaining <= TURNS_WARNING_THRESHOLD`,
apply USS class `hud--warning` to the turns label (and bar if visible).
When `turnsRemaining > TURNS_WARNING_THRESHOLD`, ensure `hud--warning` is absent.

**Timer Display Mode**

Layout elements active in this mode:
- `timer-label`: text element showing the remaining time in `MM:SS` format.
  Examples: `"02:30"`, `"00:59"`, `"00:00"`.
- `timer-label` only; no progress bar in timer mode for MVP (relies on the
  number alone for legibility).

Data source: `TimerSystem.TimeRemainingSeconds` (float).

Events subscribed:
- `TimerSystem.OnTimerTick(float timeRemainingSeconds)`: update the timer
  label. Fired by Timer System every frame (or every 0.1s — see Timer System GDD).
- `TimerSystem.OnTimerExpired()`: update to `"00:00"` and apply `hud--depleted`.

Low-time warning threshold: when `timeRemainingSeconds <= TIMER_WARNING_THRESHOLD`,
apply USS class `hud--warning` to the timer label.

**Time Formatting**

```
minutes = floor(timeRemainingSeconds / 60)
seconds = floor(timeRemainingSeconds % 60)
display = ZeroPad(minutes, 2) + ":" + ZeroPad(seconds, 2)
```

Where `ZeroPad(n, digits)` produces a string with at least `digits` characters,
left-padded with `"0"`. Examples:
- `150.7s` → `"02:30"`
- `59.0s` → `"00:59"`
- `3600.0s` → `"60:00"` (edge case; see Edge Cases)

**USS Warning Classes**

| USS Class | Applied When | Removed When |
|-----------|-------------|-------------|
| `hud--warning` | Resource below threshold | Resource rises above threshold (only possible via cheat/debug; normal play is monotone decreasing) |
| `hud--depleted` | Resource reaches 0 | Never during gameplay; cleared on next `OnGameStarted` |

The visual treatment of these classes is defined in USS (e.g., red text color,
pulsing opacity animation). This document specifies only the trigger logic.

**Turns Progress Bar Behavior**
- Fill ratio: `turnsRemaining / turnsMax`. Clamped to `[0, 1]`.
- Bar fills left to right; depletes from right to left.
- Bar color matches the label state: normal color when above warning threshold,
  warning color (red/orange) at or below threshold.
- Bar is hidden (`display: none`) when `SHOW_TURNS_PROGRESS_BAR = false`.

### States and Transitions

```
[Inactive / Hidden]
    |-- OnGameStarted        --> [Active: TurnDisplay] or [Active: TimerDisplay]

[Active: TurnDisplay]
    |-- OnTurnConsumed(N)    --> [Active: TurnDisplay] (updates label/bar)
    |-- N <= THRESHOLD       --> [Active: TurnDisplay, Warning state]
    |-- OnGameOver           --> [Active: TurnDisplay, Depleted state]
    |-- OnMainMenu           --> [Inactive / Hidden]

[Active: TimerDisplay]
    |-- OnTimerTick(t)       --> [Active: TimerDisplay] (updates label)
    |-- t <= THRESHOLD       --> [Active: TimerDisplay, Warning state]
    |-- OnTimerExpired       --> [Active: TimerDisplay, Depleted state]
    |-- OnMainMenu           --> [Inactive / Hidden]
```

Note: "Warning state" and "Depleted state" are USS class overlays, not
distinct component states — the core display mode (Turn/Timer) does not change.

### Interactions with Other Systems

**Turn Manager (Turn Display Mode only)**
- Reads `TurnManager.TurnsRemaining` and `TurnManager.TurnsMax` on init.
- Subscribes to `TurnManager.OnTurnConsumed(int turnsRemaining)`.
- Subscribes to `TurnManager.OnGameOver()` for the depleted visual state.

**Timer System (Timer Display Mode only)**
- Reads `TimerSystem.TimeRemainingSeconds` on init.
- Subscribes to `TimerSystem.OnTimerTick(float timeRemainingSeconds)`.
- Subscribes to `TimerSystem.OnTimerExpired()` for the depleted visual state.

**Game Mode Manager**
- Reads `GameModeManager.ActiveMode` on init to resolve display mode.

**Game State Machine**
- Subscribes to `OnGameStarted` to show and reset the HUD.
- Subscribes to `OnMainMenu` to hide the HUD.

---

## Formulas

**Turns progress bar fill ratio**

```
fill_ratio = clamp(turnsRemaining / turnsMax, 0.0, 1.0)
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `turnsRemaining` | int | 0–`turnsMax` | Current turns left |
| `turnsMax` | int | 1–`turnsMax` | Total turns at session start |
| `fill_ratio` | float | 0.0–1.0 | Width fraction applied to bar element |

Example: 7 turns remaining of 15 max → `fill_ratio = 7/15 = 0.467` (46.7% filled).

**Timer display formatting**

```
minutes = floor(timeRemainingSeconds / 60)
seconds = floor(timeRemainingSeconds % 60)
```

| Input | minutes | seconds | Display |
|-------|---------|---------|---------|
| 150.7 | 2 | 30 | `"02:30"` |
| 59.9 | 0 | 59 | `"00:59"` |
| 0.4 | 0 | 0 | `"00:00"` |
| 3600.0 | 60 | 0 | `"60:00"` |

Note: `floor()` is used (not `round()`) so the display never shows a value
higher than the actual remaining time.

---

## Edge Cases

**Mode is neither TurnLimit nor Timer (misconfiguration)**
- If `GameModeManager.ActiveMode` returns an unexpected value, log a warning
  and default to Turn Display Mode with a static label of `"-- : --"`. Do
  not throw an exception; present a degraded but non-crashing UI.

**Timer exceeds 99:59 (more than 99 minutes)**
- The format string `ZeroPad(minutes, 2)` will produce `"100:00"` at 6000s.
  This is fine for MVP; no session timer will exceed 10 minutes in normal design.
  No truncation or special handling needed.

**Timer System fires OnTimerTick at 0.0 before OnTimerExpired**
- When `timeRemainingSeconds` reaches 0.0, the label updates to `"00:00"` via
  `OnTimerTick`, then `OnTimerExpired` fires and applies `hud--depleted`.
  The label does not flicker because `"00:00"` is already correct by the time
  `hud--depleted` is applied.

**TurnsRemaining decrements past 0 (should not occur; defensive)**
- `fill_ratio` is clamped to `[0, 1]`. The label shows `"Turns: 0"`. No
  negative values are displayed. Board Manager should prevent submissions when
  turns are exhausted, but this is a defensive display guard.

**Rapid OnTimerTick events (every frame at 60fps)**
- UI Toolkit label updates are cheap but not free. If `OnTimerTick` fires
  every frame (60 times/second), the label text update fires 60 times/second.
  For MVP this is acceptable. If profiling shows UI thread contention, switch
  Timer System to fire `OnTimerTick` at 10Hz (every 0.1 seconds) and
  interpolate display — but this optimization is post-MVP.

**Game paused mid-timer**
- Timer System is responsible for pausing the underlying countdown. HUD
  simply stops receiving `OnTimerTick` events during pause. The displayed
  value freezes at the last received tick value. No special HUD logic needed.

**OnTurnConsumed fires with same value twice (idempotent call)**
- Updating the label to the same text it already shows is a no-op from the
  player's perspective. No special handling needed.

**Warning threshold at exactly TurnsMax (all turns = warning turns)**
- If `TURNS_WARNING_THRESHOLD >= turnsMax`, the `hud--warning` class is applied
  immediately on game start. This is valid if intentional (always show in warning
  state) but likely a misconfiguration. Log a warning on init if
  `TURNS_WARNING_THRESHOLD >= turnsMax`.

---

## Dependencies

| System | Direction | Contract |
|--------|-----------|----------|
| Turn Manager | HUD reads | Provides `TurnsRemaining`, `TurnsMax`, `OnTurnConsumed(int)`, `OnGameOver()` |
| Timer System | HUD reads | Provides `TimeRemainingSeconds`, `OnTimerTick(float)`, `OnTimerExpired()` |
| Game Mode Manager | HUD reads | Provides `ActiveMode` (enum) at initialization |
| Game State Machine | HUD responds | `OnGameStarted` shows/resets HUD; `OnMainMenu` hides HUD |

**What this system provides to others:**
- No other system reads from Turn/Timer HUD. It is a terminal presentation node.

**What this system requires from others:**
- Turn Manager or Timer System: resource update events (one or the other
  based on mode, never both active simultaneously)
- Game Mode Manager: mode flag at initialization
- Game State Machine: lifecycle events

---

## Tuning Knobs

All values live in `assets/data/ui-config.json` under the `turn_timer_hud` key.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `TURNS_WARNING_THRESHOLD` | Gate | 3 | 1–5 | Turn count at which `hud--warning` class activates. Lower = warning triggers later; higher = warning triggers earlier. |
| `TIMER_WARNING_THRESHOLD` | Gate | 30.0s | 10–60s | Seconds remaining when `hud--warning` activates for timer mode. |
| `SHOW_TURNS_PROGRESS_BAR` | Gate | true | bool | Whether the bar is shown in turn-limit mode. Disable for a cleaner minimal HUD. |

---

## Acceptance Criteria

### Functional Criteria (QA-testable)

1. **Mode resolution — turn limit**: When `GameModeManager.ActiveMode` is
   `TurnLimit`, the `turns-label` element is visible and `timer-label` is
   hidden (or absent from layout).

2. **Mode resolution — timer**: When `GameModeManager.ActiveMode` is `Timer`,
   the `timer-label` element is visible and `turns-label` is hidden.

3. **Turn label format**: After `OnTurnConsumed(7)`, the label text is exactly
   `"Turns: 7"`.

4. **Timer label format**: After `OnTimerTick(90.0)`, the label text is exactly
   `"01:30"`. After `OnTimerTick(59.9)`, the label text is exactly `"00:59"`.
   After `OnTimerTick(0.4)`, the label text is exactly `"00:00"`.

5. **Warning class — turns**: After `OnTurnConsumed(TURNS_WARNING_THRESHOLD)`,
   the `hud--warning` USS class is present on the turns-label element.
   After `OnTurnConsumed(TURNS_WARNING_THRESHOLD + 1)`, it is absent.

6. **Warning class — timer**: After `OnTimerTick(TIMER_WARNING_THRESHOLD - 0.1)`,
   the `hud--warning` USS class is present on the timer-label element.

7. **Depleted class — turns**: After `TurnManager.OnGameOver()`, the
   `hud--depleted` USS class is present on the turns-label element and
   the label reads `"Turns: 0"`.

8. **Depleted class — timer**: After `TimerSystem.OnTimerExpired()`, the
   `hud--depleted` USS class is present on the timer-label element and
   the label reads `"00:00"`.

9. **Progress bar fill — turns**: With `turnsMax = 10` and `turnsRemaining = 4`,
   the `turns-progress-bar` fill width is `40% ± 1%` of the bar's total width.

10. **HUD hidden on MainMenu**: When Game State Machine enters `MainMenu`,
    the HUD root element has `display: none` or is otherwise non-visible.

11. **HUD resets on new game**: Starting a second game session after completing
    one shows the correct initial value (full turns count or full timer) with
    no warning or depleted classes applied.

12. **UI Toolkit only**: Inspector confirms no `Canvas`, `Slider` (UGUI), or
    `Text` (UGUI) components on Turn/Timer HUD game objects.

### Experiential Criteria (Playtest)

13. **Warning state is noticed**: At least 4 of 5 playtesters notice the color
    change when the warning threshold is crossed, without being told to watch
    for it, in a structured observation session.

14. **Timer format is readable**: All 5 of 5 playtesters can immediately read
    and state the correct remaining time from the `MM:SS` display when asked,
    without needing to interpret or decode the format.

15. **Turns counter creates strategic awareness**: In a post-session debrief,
    at least 3 of 5 playtesters mention that the turn counter influenced at
    least one word selection decision (e.g., "I went for a longer word because
    I had turns to spare" or "I tried to play it safe because I was low on turns").

# Game Over Screen

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Satisfying Progression — every word found visibly changes the board

---

## Overview

The Game Over Screen is a full-screen overlay that appears at the end of a
session without unloading the game scene beneath it. It displays "Game Over"
as a header, the final score animated counting up from 0 to the true value
over approximately one second, and the total count of valid words found. Two
buttons allow the player to restart with the same mode or return to the Main
Menu. There is no high score persistence in MVP — the screen shows only the
current session's results. The screen is implemented as a UIDocument overlay
in Unity 6 UI Toolkit (UXML, USS); no UGUI components are used. It is
activated by the Game State Machine transitioning to the `GameOver` state.

---

## Player Fantasy

The Game Over Screen is the final punctuation of the session. The score
counting up from 0 transforms the final number from a fact into an event —
the player watches their accumulated achievement play out in a single
satisfying moment. Even a low score feels like a completed thing, not a
failure, because the count-up gives it ceremony. The words-found count offers
a secondary achievement angle: "I found 12 words" is an accomplishment
regardless of the point total. Both "Play Again" and "Main Menu" must be
immediately reachable — the screen should feel like the end of a round, not
a wall between sessions.

The primary MDA aesthetics this mechanic serves:
- **Sensation**: the score count-up animation transforms an abstract number
  into a cinematic reward reveal.
- **Challenge**: the final score display is the primary Competence feedback
  loop closure — the player knows how well they did.
- **Submission**: the clean, minimal layout provides closure and a clear
  path to the next session.

---

## Detailed Design

### Core Rules

**Overlay Behavior**
- The Game Over Screen is a UIDocument that renders on top of the game scene.
  The game scene (reels, HUD) remains loaded but non-interactive underneath.
- The overlay covers the full screen. A semi-transparent dark background
  (`hud-overlay--background`) visually separates the overlay from the game
  scene behind it.
- The underlying game scene elements are non-interactive while the overlay
  is active (pointer events do not pass through).

**Layout Elements**
1. **Header label**: displays `GAME_OVER_HEADER_TEXT` (default: `"Game Over"`).
2. **Score label**: animated count-up to final score (see Count-Up Animation below).
3. **Score caption**: small supplementary label, text `SCORE_CAPTION_TEXT`
   (default: `"Final Score"`), positioned above or below the score label.
4. **Words found label**: displays `"Words Found: N"` where N is the integer
   count of valid words submitted in the session.
5. **Play Again button**: primary action; restarts with the same game mode.
6. **Main Menu button**: secondary action; returns to the Main Menu.

**Data Sources**
- Final score: `ScoringSystem.SessionTotal` (int), read once when
  `OnGameOver` fires.
- Words found count: `ScoringSystem.WordsFoundCount` (int), read once when
  `OnGameOver` fires. (Alternatively, Board Manager or Turn Manager could
  own this count — see Dependencies for the contract.)
- Game mode for Play Again: `GameModeManager.SelectedMode`.

**Count-Up Animation**
When the screen appears:
1. Score label starts at `0`.
2. Over `SCORE_COUNTUP_DURATION` seconds, the displayed value increments from
   `0` to `finalScore`.
3. The increment follows a **deceleration curve** (ease-out): values increase
   quickly at first, then slow as they approach `finalScore`. This creates
   the sensation of the score "settling" into its final value.
4. The displayed value at time `t` (0 ≤ t ≤ `SCORE_COUNTUP_DURATION`):

```
progress = t / SCORE_COUNTUP_DURATION          (linear 0→1)
eased    = 1 - (1 - progress)^COUNTUP_EASE_EXP (ease-out power curve)
displayed = floor(eased * finalScore)
```

5. At `t = SCORE_COUNTUP_DURATION`, `displayed = finalScore` exactly (clamp).
6. The animation begins immediately when the overlay becomes visible —
   no additional trigger required.

**Play Again Button**
- On activation (click, tap, `Enter` when focused):
  call `GameStateMachine.RestartGame()`.
- `RestartGame()` reuses `GameModeManager.SelectedMode` from the completed
  session — the player does not return to the Main Menu to reconfigure.
- Game State Machine transitions to `Playing`, hides the Game Over Screen,
  and resets all game systems.

**Main Menu Button**
- On activation: call `GameStateMachine.GoToMainMenu()`.
- Game State Machine transitions to `MainMenu`, hides the Game Over Screen,
  shows the Main Menu.

**Button Availability During Count-Up**
- Both buttons are interactive and can be activated at any time, including
  during the count-up animation. Activating a button during the animation
  immediately ends the animation and completes the transition.
- This allows impatient players to move on without waiting; the animation
  does not gate progression.

**Keyboard Navigation**
- On screen appear, `Play Again` button receives initial focus
  (consistent with `INITIAL_FOCUS_ELEMENT` default).
- `Tab` cycles: Play Again → Main Menu → Play Again.
- `Enter` activates focused button.
- No keyboard shortcut to skip the count-up independently (activating a
  button while it is focused inherently skips the rest of the animation).

### States and Transitions

```
[Hidden]
    |-- Game State Machine enters GameOver  --> [Visible: AnimatingScore]

[Visible: AnimatingScore]
    |-- SCORE_COUNTUP_DURATION elapsed      --> [Visible: Idle]
    |-- Play Again activated                --> [Hidden] (GSM transitions to Playing)
    |-- Main Menu activated                 --> [Hidden] (GSM transitions to MainMenu)

[Visible: Idle]
    |-- Play Again activated                --> [Hidden]
    |-- Main Menu activated                 --> [Hidden]
```

### Interactions with Other Systems

**Game State Machine (bidirectional)**
- Responds to `OnEnterGameOver` to show the overlay and begin count-up.
- Responds to `OnExitGameOver` to hide the overlay.
- Dispatches `GameStateMachine.RestartGame()` on Play Again.
- Dispatches `GameStateMachine.GoToMainMenu()` on Main Menu.

**Scoring System (reads)**
- Reads `ScoringSystem.SessionTotal` (int) when `OnEnterGameOver` fires.
- Reads `ScoringSystem.WordsFoundCount` (int) when `OnEnterGameOver` fires.
- Does not subscribe to ongoing events; reads snapshot values once.

**Game Mode Manager (reads)**
- Reads `GameModeManager.SelectedMode` implicitly through the `RestartGame()`
  call (Game State Machine passes the current mode to game initialization).
  Game Over Screen does not read this directly.

---

## Formulas

**Score count-up display value at time t**

```
progress(t) = t / SCORE_COUNTUP_DURATION
eased(t)    = 1 - (1 - progress(t)) ^ COUNTUP_EASE_EXP
displayed(t) = floor(eased(t) * finalScore)
```

At `t >= SCORE_COUNTUP_DURATION`: `displayed = finalScore` (clamped, no floor needed).

| Variable | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `t` | float (seconds) | 0 – `SCORE_COUNTUP_DURATION` | — | Time elapsed since screen appeared |
| `SCORE_COUNTUP_DURATION` | float (seconds) | 0.50–2.00 | 1.00 | Total duration of the count-up animation |
| `COUNTUP_EASE_EXP` | float | 1.0–4.0 | 2.0 | Power exponent for ease-out curve. 1.0 = linear; 2.0 = quadratic ease-out; higher = more pronounced deceleration. |
| `finalScore` | int | 0 – ~9999 | — | Session total from Scoring System |
| `displayed` | int | 0 – `finalScore` | — | Value shown on screen at time t |

Example with defaults (`SCORE_COUNTUP_DURATION = 1.0`, `COUNTUP_EASE_EXP = 2.0`,
`finalScore = 100`):

| t | progress | eased | displayed |
|---|----------|-------|-----------|
| 0.00 | 0.00 | 0.000 | 0 |
| 0.25 | 0.25 | 0.438 | 43 |
| 0.50 | 0.50 | 0.750 | 75 |
| 0.75 | 0.75 | 0.938 | 93 |
| 1.00 | 1.00 | 1.000 | 100 |

The score reaches 75% of its final value by the halfway point, then decelerates
into the final value — visually satisfying and quick to read the final number.

---

## Edge Cases

**Final score is 0 (player submitted no valid words)**
- Count-up animates from 0 to 0 — it is instant with no visible change.
- `"Words Found: 0"` is displayed.
- Both buttons work normally. This edge case is functional; it may be
  aesthetically underwhelming but no special handling is needed for MVP.

**Final score is very large (unexpected)**
- Maximum realistic score: ~60 pts/word × 20 turns = 1200 pts. The label
  must accommodate 4-digit scores without layout breaking. Test at `9999`.
  Use `min-width` rather than fixed width in USS so the label expands.
- The count-up formula handles any integer without overflow at these magnitudes.

**Player presses Play Again immediately (during count-up)**
- The animation is abandoned mid-frame. The score label value at abandonment
  is irrelevant — the screen hides. No cleanup of animation state is needed
  because the component is hidden and the data will be refreshed on next
  `OnEnterGameOver`.

**Words found count diverges from actual valid submissions**
- The contract specifies reading `ScoringSystem.WordsFoundCount`. If this
  value is incorrect due to a bug in Scoring System, the display reflects
  that incorrectness. This document does not define how `WordsFoundCount`
  is tracked — that is Scoring System's responsibility. This document
  specifies what the Game Over Screen reads and when.

**Multiple rapid RestartGame calls (button double-click)**
- Debounce: after Play Again is activated, immediately set both buttons to
  `focusable = false` and apply `btn--disabled` USS class. Re-enable on
  `OnEnable` in the next session. This prevents double-submission to the
  Game State Machine.

**OnEnterGameOver fires before UIDocument is ready**
- UIDocument initialization in UI Toolkit is synchronous on `Awake`. If
  `OnEnterGameOver` fires before `Awake` (atypical but possible with event
  ordering), the component caches the event data and initializes on `OnEnable`.
  Implement with a nullable deferred-init pattern if needed.

**Session total overflows int (not realistic)**
- C# `int` max is 2,147,483,647. At 60 pts/word and ~1000 sessions, no
  overflow is possible. No special handling needed.

---

## Dependencies

| System | Direction | Contract |
|--------|-----------|----------|
| Game State Machine | Game Over Screen responds and dispatches | Receives `OnEnterGameOver` / `OnExitGameOver`; dispatches `RestartGame()`, `GoToMainMenu()` |
| Scoring System | Game Over Screen reads | Provides `SessionTotal` (int) and `WordsFoundCount` (int) as readable properties at game-over time |
| Game Mode Manager | Implicit via GSM | Game Over Screen does not read this directly; `RestartGame()` passes through Game State Machine which retains the mode |

**What this system provides to others:**
- No other system reads from Game Over Screen. It is a terminal presentation node.

**What this system requires from others:**
- Game State Machine: lifecycle events and action endpoints
- Scoring System: `SessionTotal` and `WordsFoundCount` snapshot values

---

## Tuning Knobs

All values live in `assets/data/ui-config.json` under the `game_over_screen` key.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `SCORE_COUNTUP_DURATION` | Feel | 1.00s | 0.50–2.00s | Total duration of the score count-up animation. Shorter = snappy; longer = dramatic. |
| `COUNTUP_EASE_EXP` | Feel | 2.0 | 1.0–4.0 | Exponent for ease-out curve. 1.0 = linear. Higher values spend more time near the final score (more "settling" effect). |
| `GAME_OVER_HEADER_TEXT` | Feel | `"Game Over"` | any string | Text of the header label. Change for localization or thematic variants (e.g., `"Time's Up!"` for timer mode). |
| `SCORE_CAPTION_TEXT` | Feel | `"Final Score"` | any string | Label above/below the score number. |
| `INITIAL_FOCUS_ELEMENT` | Gate | `"play-again-button"` | `"play-again-button"`, `"main-menu-button"` | Which button receives initial keyboard focus on screen appear. |

---

## Acceptance Criteria

### Functional Criteria (QA-testable)

1. **Overlay appears on GameOver state**: When Game State Machine transitions
   to `GameOver`, the Game Over Screen UIDocument root becomes visible
   (not `display: none`) and renders above the game scene.

2. **Background dims game scene**: The semi-transparent overlay background
   is visible and the underlying game scene is visually obscured (verify
   overlay USS class `hud-overlay--background` has `background-color` with
   alpha < 1.0 and > 0.0).

3. **Header text correct**: The header label text equals `GAME_OVER_HEADER_TEXT`
   (default: `"Game Over"`).

4. **Score count-up starts at 0**: The score label text is `"0"` at the
   first frame the screen is visible.

5. **Score count-up ends at finalScore**: At `SCORE_COUNTUP_DURATION` seconds
   after screen appear, the score label text equals `ScoringSystem.SessionTotal`
   as a string. Verify with `finalScore = 47`: label reads `"47"` at t=1.00s.

6. **Count-up formula — midpoint check**: With `finalScore = 100` and
   defaults, at `t = 0.50s` the displayed value is 75 (±2 tolerance for
   frame-rate rounding).

7. **Words found label correct**: The words found label text equals
   `"Words Found: " + ScoringSystem.WordsFoundCount`. Verify with
   `WordsFoundCount = 8`: label reads `"Words Found: 8"`.

8. **Play Again restarts game**: Clicking Play Again calls
   `GameStateMachine.RestartGame()`. The Game State Machine transitions to
   `Playing`. The Game Over Screen becomes hidden.

9. **Main Menu navigates**: Clicking Main Menu calls
   `GameStateMachine.GoToMainMenu()`. The Game State Machine transitions to
   `MainMenu`. The Game Over Screen becomes hidden.

10. **Buttons interactable during count-up**: Clicking Play Again at
    `t = 0.30s` (mid-animation) triggers the game restart without waiting
    for the animation to complete.

11. **Button double-click protection**: Clicking Play Again twice in rapid
    succession (< 100ms between clicks) results in exactly one call to
    `RestartGame()`.

12. **Score label accepts 4-digit scores**: Set `finalScore = 9999` in a
    test; verify the score label renders `"9999"` without truncation or
    layout overflow.

13. **UI Toolkit only**: Inspector confirms no `Canvas`, `Text` (UGUI), or
    `Button` (UGUI) components on any Game Over Screen game object.

### Experiential Criteria (Playtest)

14. **Count-up is noticed and enjoyed**: At least 4 of 5 playtesters mention
    the score counting up without prompting, in a think-aloud or post-session
    debrief. Zero playtesters describe the count-up as "annoying" or "slow."

15. **Final score is legible**: All 5 of 5 playtesters can read and state
    the correct final score within 3 seconds of the screen appearing (after
    the count-up completes), without prompting.

16. **Play Again is the path of least resistance**: At least 4 of 5 playtesters
    click Play Again rather than Main Menu when they intend to play another
    session, indicating the button hierarchy communicates primary vs secondary
    action correctly.

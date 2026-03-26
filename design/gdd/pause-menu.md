# Pause Menu

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic Clarity (the pause menu gives the player a safe moment to assess without losing game state, and destructive actions are guarded so players never lose progress accidentally)

---

## Overview

The Pause Menu is a UI Toolkit overlay that appears over the game scene during
active gameplay, accessible via the Escape key or a pause button in the HUD. When
shown, it calls `TimerSystem.Pause()` if a timer-mode session is active, freezing
all time pressure. It presents three actions: Resume (return to play), Restart
(begin a new game session of the same mode), and Main Menu (return to mode
selection). Restart and Main Menu are destructive — they discard the current
session's progress — so both require confirmation via an inline confirm dialog before
executing. Resume is the default-focused button so accidental Escape presses dismiss
rather than trigger a destructive action.

---

## Player Fantasy

Pausing should feel like a pressure-release valve: the world stops, the stakes are
held in suspension, and the player has a moment of calm agency. The confirm dialogs
for destructive actions serve a psychological function beyond preventing accidents —
they signal that the game respects the player's time and treats their session score
as something worth protecting. The confirmation moment also gives the player a chance
to reconsider ("actually, I can still beat my high score"). The target MDA aesthetics
are **Submission** (the menu imposes a pause on the experience when the player needs
relief) and **Autonomy** (the player explicitly controls whether to discard their
run).

---

## Detailed Design

### Core Rules

1. The Pause Menu can only be opened when the Game State Machine is in the `Playing`
   state. It cannot be opened from `MainMenu`, `GameOver`, or any transition state.
2. Opening the Pause Menu transitions the Game State Machine to the `Paused` state.
   The `Paused` state is responsible for calling `TimerSystem.Pause()` — the Pause
   Menu itself does not call the Timer System directly, to avoid coupling presentation
   to gameplay logic. The Game State Machine mediates this.
3. Closing the Pause Menu via Resume transitions the Game State Machine back to
   `Playing`, which calls `TimerSystem.Resume()`.
4. The Resume button receives default UI focus when the menu opens. This ensures
   Escape → Escape (open, then close) dismisses the menu without triggering any
   other action.
5. Restart and Main Menu each open a confirm dialog inline within the pause overlay.
   The confirm dialog replaces the three-button view; it shows a one-sentence
   consequence statement and two buttons: Confirm (destructive, not default-focused)
   and Cancel (returns to the three-button view, default-focused).
6. If Escape is pressed while the confirm dialog is open, the dialog closes and the
   three-button view is restored (same behavior as pressing Cancel).
7. If Escape is pressed while the three-button view is shown, the menu closes and
   the game resumes (same as pressing Resume).
8. The pause overlay renders above the game scene using a UI Toolkit `PanelSettings`
   sort order higher than the game HUD. The game scene remains visible but
   non-interactive behind the overlay (input is blocked by the overlay's hit-testing).
9. The pause button in the HUD (if present) is hidden or disabled while the Pause
   Menu is open, to prevent double-open edge cases.
10. During the confirm dialog for Restart: on Confirm, the current session is
    discarded, the current game mode is re-initialized, and a new game session starts.
    The Game State Machine transitions `Paused → Playing` with a fresh game.
11. During the confirm dialog for Main Menu: on Confirm, the current session is
    discarded and the Game State Machine transitions `Paused → MainMenu`. No score
    is saved (game over save only occurs on natural expiry, not on quit-to-menu).

### States and Transitions

The Pause Menu has three internal UI views (not full states — these are view modes
within the single `Paused` game state):

```
[Game Playing]
     │
  Escape / HUD pause button
     │
     ▼
[Pause Menu: Three-Button View]
  - Resume (default focus)        ──► [Game Playing]
  - Restart                        ──► [Confirm: Restart View]
  - Main Menu                      ──► [Confirm: Main Menu View]
     │
  Escape
     │
     ▼
[Game Playing]

[Confirm: Restart View]
  - Confirm (not default)          ──► [Game Playing, new session]
  - Cancel (default focus)         ──► [Pause Menu: Three-Button View]
     │
  Escape
     │
     ▼
[Pause Menu: Three-Button View]

[Confirm: Main Menu View]
  - Confirm (not default)          ──► [Main Menu Scene]
  - Cancel (default focus)         ──► [Pause Menu: Three-Button View]
     │
  Escape
     │
     ▼
[Pause Menu: Three-Button View]
```

### Confirm Dialog Content

**Restart confirmation:**
- Prompt: "Start a new game? Your current score will be lost."
- Confirm button label: "Restart"
- Cancel button label: "Keep Playing"

**Main Menu confirmation:**
- Prompt: "Return to the main menu? Your current score will not be saved."
- Confirm button label: "Main Menu"
- Cancel button label: "Keep Playing"

### Interactions with Other Systems

**Game State Machine**: The Pause Menu communicates exclusively through the Game
State Machine. It sends `RequestPause`, `RequestResume`, `RequestRestart`, and
`RequestMainMenu` signals. The Game State Machine owns all timer coordination and
scene transitions.

**Timer System**: The Timer System is paused and resumed by the Game State Machine
in response to state transitions, not by the Pause Menu directly. This preserves
the single-responsibility principle and prevents the Pause Menu from needing to
know whether a timer session is active.

**HUD (Turn/Timer HUD)**: The HUD pause button sends the same `RequestPause` signal
as the Escape key. The HUD itself may hide or disable the pause button while the
`Paused` state is active (the HUD reads Game State Machine state for this).

**Input System**: The Pause Menu intercepts the Escape key using Unity's Input System
action map. When the game is in `Playing` state, the Escape action triggers pause.
When in `Paused` state, the Escape action triggers the dismiss/cancel logic described
above. The action map switches to a `UI` map when paused, disabling gameplay input.

---

## Formulas

The Pause Menu contains no mathematical formulas. It is purely a state-routing and
input-handling system. The relevant logic is decision trees, not equations.

**Input routing pseudocode:**

```
on EscapePressed:
    if GameState == Playing:
        GameStateMachine.RequestPause()
    else if GameState == Paused:
        if ConfirmDialogOpen:
            CloseConfirmDialog()   // restore Three-Button View
        else:
            GameStateMachine.RequestResume()

on ResumeButton.Clicked:
    GameStateMachine.RequestResume()

on RestartButton.Clicked:
    ShowConfirmDialog(type: Restart)

on MainMenuButton.Clicked:
    ShowConfirmDialog(type: MainMenu)

on ConfirmButton.Clicked (Restart):
    GameStateMachine.RequestRestart()

on ConfirmButton.Clicked (MainMenu):
    GameStateMachine.RequestMainMenu()

on CancelButton.Clicked:
    CloseConfirmDialog()
```

---

## Edge Cases

**Escape during scene transition**: If the player presses Escape while a scene load
is in progress, the input must be ignored. The Game State Machine is in a transition
state that is not `Playing`, so the guard on rule 1 covers this.

**Pause button pressed while confirm dialog is open**: The HUD pause button should
be inactive while `Paused` state is active (rule 9). If it somehow fires (race
condition), treat it as a no-op — the Pause Menu is already open.

**Rapid double-tap of Escape**: Escape open → immediate Escape close. The `Playing
→ Paused → Playing` round-trip must not cause any timer desync. The timer is paused
in `OnEnterPaused()` and resumed in `OnEnterPlaying()` by the Game State Machine,
not on a per-frame basis, so rapid transitions are safe.

**Restart in turn-limit mode**: A new session starts with full turns restored. The
current session's score is discarded with no save. This is consistent with rule 11
(save only on natural game over).

**Restart in timer mode**: A new session starts with `TimeRemaining = StartDuration`
and a fresh timer. The frozen sub-state is reset. `LowTimeFired` is reset to false.

**Confirm dialog already open when Restart/Main Menu button pressed again**: Not
possible under normal UI flow, as the three-button view is hidden when the confirm
dialog is shown. Guard against it anyway with a `_confirmDialogOpen` bool.

**Game Over occurs during pause** (e.g., a future system where time could expire
while paused — currently impossible because the timer is paused): The `OnTimerExpired`
event fires only when the timer is Running. Since the timer is Paused during the
Pause Menu, `OnTimerExpired` cannot fire. This edge case is structurally prevented.

**Very low time remaining when resuming**: The player pauses at 1 second remaining,
reads the board carefully, resumes. This is intentional — pausing is always allowed,
and there is no penalty for pausing. If this is deemed exploitable in timer mode, a
design revision could introduce a "pause budget" (e.g., 3 pauses per game), but this
is not in the current design and should not be added without playtesting evidence.

---

## Dependencies

### This system requires from others

| System | What it needs |
|--------|--------------|
| Game State Machine | `Playing` state check before opening; `RequestPause()`, `RequestResume()`, `RequestRestart()`, `RequestMainMenu()` signal methods |
| Timer System | No direct dependency — coordinated through Game State Machine |
| Unity Input System | Escape key action, action map switching between `Gameplay` and `UI` maps |
| UI Toolkit | Overlay `VisualElement` with sort order above HUD; focus management for default-focused buttons |

### This system provides to others

| System | What it provides |
|--------|-----------------|
| Game State Machine | User intent signals: `RequestPause`, `RequestResume`, `RequestRestart`, `RequestMainMenu` |
| HUD | `IsPauseMenuOpen` (bool) — so the HUD pause button can disable itself |

---

## Tuning Knobs

The Pause Menu has no numeric tuning knobs. Its behavior is fully defined by the
state routing rules above. The following are implementation constants (not tuning
values) that should nevertheless live in configuration rather than hardcoded strings:

| Constant | Type | Value | Notes |
|----------|------|-------|-------|
| `RestartConfirmPrompt` | string | "Start a new game? Your current score will be lost." | Localization key in production. |
| `MainMenuConfirmPrompt` | string | "Return to the main menu? Your current score will not be saved." | Localization key in production. |
| `RestartConfirmLabel` | string | "Restart" | Button label. |
| `MainMenuConfirmLabel` | string | "Main Menu" | Button label. |
| `CancelLabel` | string | "Keep Playing" | Used in both confirm dialogs. |

These strings live in `assets/data/ui-strings.json` for localization readiness, even
though localization is not in the current project scope.

---

## Acceptance Criteria

### Functional

- [ ] Escape key during `Playing` state opens the Pause Menu.
- [ ] Escape key during `Paused` state with no confirm dialog open closes the Pause Menu and resumes the game.
- [ ] Escape key during `Paused` state with confirm dialog open closes the confirm dialog (returns to three-button view), does not resume the game.
- [ ] Resume button closes the Pause Menu and resumes the game without showing a confirm dialog.
- [ ] Restart button opens the confirm dialog with the correct prompt and "Restart" / "Keep Playing" labels.
- [ ] Confirm on Restart starts a new session of the same mode; the score resets to zero; turns/timer resets to configured values.
- [ ] Cancel on Restart returns to the three-button view; the game remains paused.
- [ ] Main Menu button opens the confirm dialog with the correct prompt and "Main Menu" / "Keep Playing" labels.
- [ ] Confirm on Main Menu transitions to the Main Menu scene; no score is saved.
- [ ] Cancel on Main Menu returns to the three-button view; the game remains paused.
- [ ] The timer does not count down while the Pause Menu is open (timer-mode verification).
- [ ] The Resume button receives focus when the Pause Menu opens (keyboard/gamepad navigation starts on Resume).
- [ ] Cancel button receives focus when a confirm dialog opens.
- [ ] Pause Menu cannot be opened from the Main Menu or Game Over Screen.
- [ ] Rapid Escape open/close cycle (10 times) does not cause timer desync (timer value before pause equals timer value after resume, within ±1 frame).

### Experiential (Playtest Validation)

- [ ] No playtester accidentally triggers Restart or Main Menu without intending to (confirm dialog effectiveness).
- [ ] Playtesters can open and dismiss the pause menu in under 2 seconds without reading any instructions.
- [ ] Playtesters in timer mode report that pausing "feels safe" and does not create anxiety about losing time.

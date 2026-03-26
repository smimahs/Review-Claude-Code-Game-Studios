# Main Menu

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Replayability through Determinism — clear, repeatable entry point

---

## Overview

The Main Menu is the entry point for every play session. It displays the game
title, a mutually exclusive mode selector (Turn Limit / Timer), a Start button
that launches the selected mode, and a visually disabled High Scores button
that signals future content without delivering it in MVP. The menu is implemented
as a full-screen UIDocument using Unity 6 UI Toolkit (UXML layout, USS styling);
no UGUI components are used. It is driven entirely by the Game State Machine:
it appears when the state is `MainMenu` and is hidden when the state transitions
to `Playing`.

---

## Player Fantasy

The main menu should feel like the front of a physical slot machine at a
casino — confident, atmospheric, and inviting. The player should understand
within two seconds what the game is, how to start, and that there is a
meaningful choice to make before beginning. The Turn Limit / Timer selector
should feel like choosing a game mode on a pinball machine's attract screen —
a small ritual that primes the player for the kind of session they are about
to have. Everything else is stripped away: no options menus, no login walls,
no friction between the player and their first word.

The primary MDA aesthetic this mechanic serves:
- **Fantasy**: the title presentation and aesthetic communicate the game's
  identity and set the player's expectations.
- **Submission**: the menu is minimal by design — it provides exactly the
  structure the player needs to relax into the experience without cognitive
  overhead.

---

## Detailed Design

### Core Rules

**Layout Elements**
The menu contains exactly five interactive and display elements:

1. **Title label** — game title text, non-interactive.
2. **Mode selector: Turn Limit button** — mutually exclusive mode choice.
3. **Mode selector: Timer button** — mutually exclusive mode choice.
4. **Start button** — launches the game in the selected mode.
5. **High Scores button** — disabled in MVP; shows tooltip on hover/focus.

No other elements exist in MVP (no Settings, no Credits, no Profile).

**Mode Selector Behavior**
The two mode buttons form a radio group. Exactly one is selected at all times.
- Default selected mode on first load: `Turn Limit` (see Tuning Knobs
  `DEFAULT_MODE`).
- On subsequent menu visits within the same application session: the last
  selected mode is retained (not persisted to disk in MVP — session memory
  only via `GameModeManager.SelectedMode`).
- Selecting a mode: click/tap its button. The selected button receives USS
  class `mode-btn--selected`. The other button loses `mode-btn--selected`.
- Keyboard navigation: `Tab` / `Shift+Tab` cycle focus through all interactive
  elements. `Enter` or `Space` activates the focused element.
- The mode selector communicates the selection to `GameModeManager.SetMode(GameMode)`
  immediately on click, before the Start button is pressed.

**Start Button Behavior**
- Always enabled when a mode is selected (which is always, per above rules).
- On activation (click, tap, or `Enter` when focused): call
  `GameStateMachine.StartGame()`.
- Game State Machine transitions to `Playing`, which triggers Scene Flow to
  load the game scene and hide the Main Menu.

**High Scores Button Behavior**
- Visual state: disabled appearance (reduced opacity, USS class `btn--disabled`).
- Not focusable via `Tab` in MVP (set `focusable = false` in UXML).
- On hover/pointer-enter: display a tooltip label with text defined by
  `HIGH_SCORES_TOOLTIP_TEXT` (default: `"Coming Soon"`).
- On click/tap: no action. The button appearance does not change on press.
  Do not call any function. Optionally log a debug message for analytics.
- The tooltip is a child element of the button with `display: none` toggled
  to `display: flex` on `PointerEnterEvent` and back on `PointerLeaveEvent`.

**First-Load State**
On `OnEnable` (first time menu appears), the menu initializes with:
- `Turn Limit` mode button bearing `mode-btn--selected` class (or last session
  mode if `GameModeManager.SelectedMode` is already set).
- Start button enabled.
- High Scores button disabled.
- No tooltip visible.

### States and Transitions

The Main Menu component has a simple two-state lifecycle:

```
[Hidden]
    |-- Game State Machine enters MainMenu state --> [Visible]

[Visible]
    |-- Start button activated                   --> [Hidden] (GSM transitions to Playing)
```

Within `[Visible]`, the mode selector transitions freely between Turn Limit
and Timer selection states. These are sub-states of `[Visible]`, not
component-level states.

```
[Visible / TurnLimit selected]    <-->    [Visible / Timer selected]
    (toggled by clicking the mode buttons)
```

### Interactions with Other Systems

**Game State Machine (bidirectional)**
- Responds to `OnEnterMainMenu` to show the menu.
- Responds to `OnExitMainMenu` to hide the menu.
- Dispatches `GameStateMachine.StartGame()` on Start button activation.

**Game Mode Manager**
- Dispatches `GameModeManager.SetMode(GameMode.TurnLimit)` or
  `GameModeManager.SetMode(GameMode.Timer)` when the player changes mode
  selection.
- Reads `GameModeManager.SelectedMode` on `OnEnable` to restore last selection.

---

## Formulas

The Main Menu contains no mathematical formulas. It is a pure presentation
and navigation component. All logic is conditional (if/else) rather than
numeric.

---

## Edge Cases

**Player mashes Start before the mode is set (impossible by design)**
- A mode is always pre-selected (default to `Turn Limit`). There is no
  "no mode" state. The Start button cannot be clicked when no mode is active
  because a mode is always active. No special handling needed.

**Keyboard Enter pressed with no focused element**
- Unity UI Toolkit does not fire keyboard activation events without a focused
  element. In practice, the Start button should receive initial focus on menu
  appear (see `INITIAL_FOCUS_ELEMENT` Tuning Knob). If focus is lost (e.g.,
  the player clicked outside all interactive elements), Enter does nothing.
  This is acceptable behavior.

**Player clicks High Scores button repeatedly**
- No action fires; no state changes. Rapid clicks are no-ops. The tooltip
  remains visible as long as the pointer is over the button.

**Application loses focus while menu is open (alt-tab, etc.)**
- No menu-specific handling needed. Unity pauses or runs in background per
  application settings. On refocus, menu state is unchanged.

**Tooltip displays on touch devices (no hover)**
- On touch devices, `PointerEnterEvent` fires on tap. The tooltip appears
  on the first tap but the click action (second tap to "activate") does nothing.
  This is acceptable. For post-MVP, replace with a "?" info button or remove
  the disabled button entirely.

**Mode selection on return from Game Over**
- When the player navigates from Game Over Screen back to Main Menu, the
  Game Mode Manager retains the mode used in the previous session. The menu
  restores that selection on `OnEnable`. The player can change mode before
  starting again.

**Single-frame flash of incorrect default state**
- Initialize USS classes in `OnEnable` synchronously before the first render
  frame. Do not set defaults in `Start()` (which may fire one frame late in
  UIDocument lifecycle). This prevents a one-frame flash of un-styled buttons.

---

## Dependencies

| System | Direction | Contract |
|--------|-----------|----------|
| Game State Machine | Main Menu responds and dispatches | Receives `OnEnterMainMenu` / `OnExitMainMenu`; dispatches `StartGame()` |
| Game Mode Manager | Main Menu reads and dispatches | Reads `SelectedMode` on init; dispatches `SetMode(GameMode)` on selection change |

**What this system provides to others:**
- No other system reads from Main Menu. It is a terminal presentation node.

**What this system requires from others:**
- Game State Machine: state change events to show/hide the menu
- Game Mode Manager: current mode for initialization and mode change dispatch

---

## Tuning Knobs

All values live in `assets/data/ui-config.json` under the `main_menu` key.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `DEFAULT_MODE` | Gate | `"TurnLimit"` | `"TurnLimit"` or `"Timer"` | Which mode button is selected when no prior session mode exists. |
| `HIGH_SCORES_TOOLTIP_TEXT` | Feel | `"Coming Soon"` | any string | Text shown when hovering the disabled High Scores button. |
| `INITIAL_FOCUS_ELEMENT` | Gate | `"start-button"` | `"start-button"`, `"mode-turnlimit"`, `"mode-timer"` | Which element receives keyboard focus when the menu appears. Default directs Enter key directly to Start. |

---

## Acceptance Criteria

### Functional Criteria (QA-testable)

1. **Menu appears on MainMenu state**: When Game State Machine enters
   `MainMenu`, the Main Menu UIDocument root element is visible (not
   `display: none`).

2. **Menu hidden on Playing state**: When Game State Machine transitions to
   `Playing`, the Main Menu UIDocument root element is hidden.

3. **Default mode selected**: On first menu appearance, the Turn Limit button
   bears USS class `mode-btn--selected` and the Timer button does not (assuming
   default `DEFAULT_MODE = "TurnLimit"`).

4. **Mode toggle**: Clicking the Timer button results in Timer button bearing
   `mode-btn--selected` and Turn Limit button losing it. `GameModeManager.SelectedMode`
   equals `GameMode.Timer`.

5. **Mode toggle back**: Clicking Turn Limit after Timer is selected restores
   Turn Limit to `mode-btn--selected` and `GameModeManager.SelectedMode`
   equals `GameMode.TurnLimit`.

6. **Start launches game**: Clicking Start calls `GameStateMachine.StartGame()`.
   The Game State Machine transitions to `Playing` (verify state value directly).

7. **High Scores button disabled**: The High Scores button has USS class
   `btn--disabled` applied and `focusable = false`. Clicking it does not
   change any game state and does not call any method (verify via mock/spy).

8. **Tooltip appears on hover**: Moving the pointer over the High Scores button
   makes the tooltip child element visible. Moving the pointer away hides it.

9. **Keyboard Start activation**: When the Start button has focus, pressing
   `Enter` triggers the same behavior as a mouse click (game starts).

10. **Keyboard Tab navigation**: Pressing `Tab` from the Turn Limit button
    cycles through: Timer button → Start button → (High Scores button is
    skipped because `focusable = false`) → back to Turn Limit (or cycle order
    defined by UXML tab index).

11. **Mode persists across sessions (within app run)**: Start a Turn Limit
    game, complete it, return to Main Menu. The Turn Limit button is
    pre-selected. Switch to Timer, return to Main Menu again: Timer is
    pre-selected.

12. **UI Toolkit only**: Inspector confirms no `Canvas`, `Button` (UGUI), or
    `Text` (UGUI) components on any Main Menu game object.

### Experiential Criteria (Playtest)

13. **Two-second comprehension**: All 5 of 5 playtesters can state the game's
    name, the mode options available, and how to start the game within 2
    seconds of the menu appearing, without reading any instructions.

14. **Mode choice feels meaningful**: At least 4 of 5 playtesters state they
    understand the difference between Turn Limit and Timer mode from the menu
    labels alone (without tooltips or descriptions), confirmed in post-session
    debrief.

15. **High Scores legibly unavailable**: All 5 of 5 playtesters recognize
    the High Scores button as inactive/unavailable without clicking it,
    based on its visual appearance alone. Zero playtesters express confusion
    about why clicking it does nothing after seeing its disabled state.

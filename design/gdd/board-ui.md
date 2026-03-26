# Board UI

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic Clarity — board state always legible

---

## Overview

The Board UI is the primary visual surface of ReelWords. It renders six reel
"windows" arranged horizontally in a slot machine aesthetic, each displaying
the current character of its corresponding Reel Controller. The component
manages three visual states per reel (unselected, selected, animating), enforces
the contiguous-selection rule through click/tap interaction logic, and composes
the live word preview string from the ordered set of selected reels. It is a
pure presentation layer: it reads state from Board Manager and Reel Controller,
dispatches selection events to Board Manager, and never owns game logic itself.
All rendering is implemented in Unity 6 UI Toolkit using UIDocument, UXML
layout, and USS styling; no UGUI components are used.

---

## Player Fantasy

The player should feel like they are operating a real mechanical slot machine —
pressing physical drum faces to lock them into a word. Each reel face should
feel weighty and tactile. When a selection is made, the highlighted reels should
glow or pop visually so the player always knows at a glance which letters they
have claimed. When a word is submitted and accepted, the advancing reels should
spin with a brief satisfying animation before settling on the next character,
reinforcing the core fantasy that every word physically transforms the machine.

The primary MDA aesthetics this mechanic serves:
- **Sensation**: the slot machine spin animation and selection highlight create
  moment-to-moment physical pleasure.
- **Challenge**: legibility of the board state is a prerequisite for strategic
  decision-making; the UI must never obscure which letters are available.

---

## Detailed Design

### Core Rules

**Reel Layout**
- Six reel windows are arranged in a single horizontal row, evenly spaced.
- Each reel window displays exactly one character: `ReelController.CurrentChar`
  for the reel at that index (0–5).
- Reel windows are indexed 0 (leftmost) through 5 (rightmost).
- The character displayed is always the uppercase form of `CurrentChar`.

**Selection States**
Each reel window exists in exactly one of three visual states at any time:

| State | Trigger | Visual Treatment |
|-------|---------|-----------------|
| `Unselected` | Default; reel not in current selection | Normal background, no border highlight |
| `Selected` | Reel index is in the active selection set | Highlighted border and elevated background tint |
| `Animating` | Reel just advanced after a valid word submission | Spin/roll animation plays; letter blurs upward and new letter settles in |

**Contiguous Selection Rule**
The player may only select a contiguous (unbroken) range of reels. The
selection is described by two indices: `selectionStart` and `selectionEnd`,
where `selectionStart <= selectionEnd` and all indices in
`[selectionStart, selectionEnd]` are selected.

**Click / Tap Interaction Logic**

When the player clicks reel at index `N`:

1. If no reels are currently selected:
   - Begin new selection: `selectionStart = N`, `selectionEnd = N`.

2. If reels are currently selected (range `[S, E]`):
   a. If `N == E + 1` (extends right): set `selectionEnd = N`.
   b. If `N == S - 1` (extends left): set `selectionStart = N`.
   c. If `N == E` (deselects rightmost): set `selectionEnd = N - 1`.
      If `selectionEnd < selectionStart`, clear the entire selection.
   d. If `N == S` (deselects leftmost): set `selectionStart = N + 1`.
      If `selectionStart > selectionEnd`, clear the entire selection.
   e. If `N` is within `(S, E)` exclusive: clear selection and begin new
      selection at `N` (`selectionStart = N`, `selectionEnd = N`).
   f. If `N` is outside `[S-1, E+1]` (non-adjacent, not extending):
      clear selection and begin new selection at `N`.

This means a non-adjacent click always clears the prior selection and starts
fresh at the tapped reel. There is no multi-range selection.

**Live Word Preview**
The Board UI constructs the live word string by reading
`ReelController.CurrentChar` for each index in `[selectionStart, selectionEnd]`
in ascending order and concatenating them. This string is passed to the Word
Input UI component for display (see Word Input UI dependency).

**Advance Animation**
When Board Manager fires `OnWordAccepted(int[] advancedReelIndices)`:
1. Each reel at an index in `advancedReelIndices` enters the `Animating` state.
2. The animation plays for exactly `REEL_SPIN_DURATION` seconds (see Tuning
   Knobs).
3. At the midpoint of the animation (`REEL_SPIN_DURATION / 2`), the displayed
   character transitions to the new `CurrentChar` (the character is invisible
   at this midpoint due to the scroll position of the animation).
4. After `REEL_SPIN_DURATION`, the reel returns to either `Unselected` or
   `Selected` depending on whether it remains in the active selection.
5. Board UI does not clear the selection automatically on word acceptance;
   that is Board Manager's responsibility via `OnSelectionChanged`.

### States and Transitions

```
[Unselected]
    |-- player clicks this reel (contiguous) --> [Selected]
    |-- receives Animating signal              --> [Animating]

[Selected]
    |-- player clicks to deselect             --> [Unselected]
    |-- receives Animating signal             --> [Animating]
    |-- player clicks non-adjacent reel       --> [Unselected] (selection cleared)

[Animating]
    |-- animation timer expires               --> [Unselected] (default after advance)
```

The `Animating` state is a transient overlay; the reel's underlying
selected/unselected membership does not change during animation. After the
animation completes, the reel resolves to its post-advance selection state
as dictated by Board Manager.

### Interactions with Other Systems

**Board Manager (reads and responds to)**
- Reads `BoardManager.GetCurrentChar(reelIndex)` on every frame during
  `Animating` state to update the displayed character at the animation midpoint.
- Subscribes to `BoardManager.OnSelectionChanged(int[] selectedIndices)` to
  update the visual state of all six reel windows.
- Subscribes to `BoardManager.OnWordAccepted(int[] advancedReelIndices)` to
  trigger advance animations.
- Dispatches `BoardManager.SetSelection(int start, int end)` when the player
  clicks a reel (Board Manager enforces the authoritative selection state;
  Board UI is notified back through `OnSelectionChanged`).

**Reel Controller (reads)**
- Reads `ReelController.CurrentChar` for each of the six reels to populate
  the displayed character in each reel window.
- Does not write to Reel Controller.

**Word Input UI (provides)**
- Provides the current live word string (concatenation of selected reel chars)
  to Word Input UI via a shared observable or direct component reference. Board
  UI owns the composition; Word Input UI only displays it.

---

## Formulas

Board UI is a pure presentation layer. It contains no game-logic formulas.
The only numeric relationships are timing-related:

**Animation midpoint character swap**

```
swap_time = REEL_SPIN_DURATION * REEL_SPIN_MIDPOINT_RATIO
```

| Variable | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `REEL_SPIN_DURATION` | float (seconds) | 0.15 – 0.60 | 0.30 | Total duration of the spin animation per reel |
| `REEL_SPIN_MIDPOINT_RATIO` | float (0–1) | 0.40 – 0.60 | 0.50 | Fraction through animation at which character swaps |
| `swap_time` | float (seconds) | derived | 0.15 | Moment the new character becomes visible |

Example: with defaults, `swap_time = 0.30 * 0.50 = 0.15s`. The old character
is visible from 0s to 0.15s, hidden from 0.10s to 0.20s (blur/scroll peak),
and the new character appears from 0.15s onward, settling by 0.30s.

**Staggered animation offset (optional visual polish)**

If multiple reels animate simultaneously, each reel `i` in
`advancedReelIndices` (sorted ascending) may start its animation at:

```
stagger_offset(i) = i * REEL_STAGGER_DELAY
```

| Variable | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `REEL_STAGGER_DELAY` | float (seconds) | 0.00 – 0.08 | 0.04 | Per-reel delay offset for visual cascade |

Example: a 3-reel word spanning reels 1-3 would stagger at 0.04s, 0.08s, and
0.12s respectively. Set to 0 to disable staggering.

---

## Edge Cases

**All 6 reels selected**
- Valid. `selectionStart = 0`, `selectionEnd = 5`. All reel windows enter
  `Selected` state. Submit is enabled. This is the maximum selection size.

**Single reel selected**
- Valid selection state (one reel highlighted), but Word Input UI's Submit
  button remains disabled because minimum word length is 2. The Board UI
  itself does not disable anything; it reports the selection faithfully.

**Player clicks the only selected reel (deselect all)**
- Case 2c or 2d above applies. `selectionStart` and `selectionEnd` converge
  past each other, triggering a full selection clear. All reels enter
  `Unselected` state.

**Rapid repeated clicks on the same reel**
- Each click is processed as a discrete event. If the same reel is clicked
  twice in quick succession, the first click selects it (if unselected) and
  the second click deselects it. No debounce is applied; this is intentional
  — players who accidentally double-click get instant feedback.

**Animation interruption (player submits during spin)**
- Board Manager will not accept a new submission while any reel is in
  `Animating` state. Submit is disabled by Word Input UI during this window.
  Board UI does not need to handle this case independently, but must expose
  `IsAnyReelAnimating()` so Word Input UI can query it.

**All reels advancing simultaneously (6-letter word)**
- All 6 reels enter `Animating` state. If `REEL_STAGGER_DELAY > 0`, reel 0
  begins first, reel 5 begins last at `5 * REEL_STAGGER_DELAY = 0.20s`.
  Total animation window: `REEL_SPIN_DURATION + 5 * REEL_STAGGER_DELAY`.
  With defaults: `0.30 + 0.20 = 0.50s`. Input is blocked for this duration.

**Reel character is a non-alphabetic symbol (future content)**
- Display the symbol as-is. The validation layer (Word Validator) handles
  legality; Board UI renders whatever character `CurrentChar` returns.

**Very fast clicks during board initialization**
- Board Manager fires `OnSelectionChanged` with an empty array on initialization.
  Board UI processes this on `OnEnable` to guarantee a clean `Unselected`
  state even if the component was recycled from a previous session.

---

## Dependencies

| System | Direction | Contract |
|--------|-----------|----------|
| Board Manager | Board UI reads/subscribes | Provides `OnSelectionChanged`, `OnWordAccepted`, `SetSelection(start, end)`, `GetCurrentChar(index)` |
| Reel Controller | Board UI reads | Provides `CurrentChar` (char) per reel instance |
| Word Input UI | Board UI writes | Receives live word string; Board UI pushes on every selection change |
| Game State Machine | Board UI responds | Board UI disables interaction (all reels non-clickable) when state is not `Playing` |

**What this system provides to others:**
- `IsAnyReelAnimating()` — bool; queried by Word Input UI to gate Submit
- `GetReelScreenRect(int reelIndex)` — Rect (screen space); queried by Score UI to compute flyout spawn position. Returns the bounding rect of the reel window for the given index (0–5).
- Live word string — pushed to Word Input UI on every selection change
- Visual reel state — purely local; no other system reads it

**What this system requires from others:**
- Board Manager: selection events and word-accepted events
- Reel Controller: `CurrentChar` for each reel
- Game State Machine: current state to enable/disable interaction

---

## Tuning Knobs

All values live in `assets/data/ui-config.json` under the `board_ui` key.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `REEL_SPIN_DURATION` | Feel | 0.30s | 0.15–0.60s | Speed of the reel advance animation. Below 0.15s feels instant and loses tactile feedback. Above 0.60s feels laggy. |
| `REEL_SPIN_MIDPOINT_RATIO` | Feel | 0.50 | 0.40–0.60 | When in the animation the new character is revealed. 0.50 = midpoint. Lower values show the new char earlier. |
| `REEL_STAGGER_DELAY` | Feel | 0.04s | 0.00–0.08s | Cascade delay between advancing reels. Set to 0 to disable. Higher values create a more theatrical cascade. |
| `SELECTED_BORDER_WIDTH` | Feel | 3px | 2–6px | Thickness of the selection highlight border in USS. |
| `SELECTED_TINT_ALPHA` | Feel | 0.25 | 0.15–0.50 | Opacity of the selection background color overlay. |

---

## Acceptance Criteria

### Functional Criteria (QA-testable)

1. **Six reels render**: At game start, exactly 6 reel windows are visible,
   each displaying the correct `CurrentChar` for its reel index. Verify by
   reading `ReelController.CurrentChar` for all 6 and comparing to display.

2. **Contiguous selection — extend right**: With reels 2-3 selected, clicking
   reel 4 results in reels 2-4 selected and reels 0-1 and 5 unselected.

3. **Contiguous selection — non-adjacent clears**: With reels 2-3 selected,
   clicking reel 5 results in only reel 5 selected and all others unselected.

4. **Deselect single reel**: With only reel 2 selected, clicking reel 2 results
   in zero reels selected.

5. **Advance animation triggers**: After a valid word submission accepted by
   Board Manager, every reel whose index is in `advancedReelIndices` plays the
   spin animation. Verify `Animating` state enters and exits within
   `REEL_SPIN_DURATION + 5 * REEL_STAGGER_DELAY + 0.05s` tolerance.

6. **Character swap at midpoint**: During animation, the displayed character
   changes to the new `CurrentChar` at `swap_time` (±1 frame tolerance at 60fps).

7. **Interaction disabled during animation**: Clicking any reel while any reel
   is `Animating` produces no selection change. Verify `SetSelection` is not
   called on Board Manager.

8. **All-6 selection**: Clicking reels 0 through 5 in sequence results in all
   6 entering `Selected` state.

9. **UI Toolkit only**: Inspector confirms no `Canvas`, `Image` (UGUI), or
   `Button` (UGUI) components on any Board UI game object. All elements are
   `UIDocument` with UXML/USS.

### Experiential Criteria (Playtest)

10. **Selection always legible**: A playtester should be able to identify which
    reels are selected from across a normal viewing distance (60–80cm from a
    1080p monitor) without uncertainty. Target: 100% of playtesters identify
    correctly on first inspection, no prompt needed.

11. **Spin animation feels satisfying**: At least 4 of 5 playtesters describe
    the reel advance animation as "satisfying," "crisp," or "responsive" (not
    "slow," "laggy," or "janky") in a structured think-aloud session.

12. **Slot machine aesthetic recognized**: At least 3 of 5 playtesters
    spontaneously use the words "slot machine," "reel," or "spinning" when
    describing the board without prompting.

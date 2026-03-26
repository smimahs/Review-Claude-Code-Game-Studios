# Word Input UI

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic Clarity — board state always legible

---

## Overview

The Word Input UI presents and controls the word submission flow. It displays
the live word being composed from selected reels as a text preview, provides
Submit and Clear action buttons, and delivers immediate visual and animated
feedback on the outcome of a submission. It is a pure presentation and
interaction layer: it reads the live word string from Board UI, dispatches
submission and clear intents to Board Manager, and reflects validation results
through USS class toggling and a CSS-driven shake animation. No game logic
lives in this component. All rendering is implemented in Unity 6 UI Toolkit
using UIDocument, UXML, and USS; no UGUI components are used.

---

## Player Fantasy

The player should feel confident and in control at the moment of submission.
The live word preview makes the intent explicit before committing, eliminating
ambiguity. When a word is accepted, a brief green flash rewards the decision
like a correct answer on a quiz show. When a word is rejected, the red flash
and shake communicate the error instantly and unmistakably — frustrating
enough to register, brief enough to not slow the player down. The Clear button
is always within reach, acting as an escape hatch that keeps the player moving
without penalty to board state.

The primary MDA aesthetics this mechanic serves:
- **Challenge**: clear submission feedback closes the loop between player
  decision and game response, satisfying the Competence need from
  Self-Determination Theory.
- **Sensation**: the green flash and shake animation create micro-pleasure
  and micro-tension at the highest-frequency interaction point in the game.

---

## Detailed Design

### Core Rules

**Word Preview Display**
- The word preview element displays the live word string composed by Board UI
  from the currently selected reels.
- When zero reels are selected, the preview displays an empty string or a
  placeholder (see Tuning Knobs for `WORD_PREVIEW_PLACEHOLDER_TEXT`).
- The preview updates synchronously on every `OnSelectionChanged` event —
  there is no polling or delay.
- Characters are displayed in reel index order, left to right, uppercase.

**Submit Button**
- Enabled when: the live word string has length >= `MIN_WORD_LENGTH` (default: 2)
  AND no reel is currently in `Animating` state (queried via
  `BoardUI.IsAnyReelAnimating()`).
- Disabled otherwise. A disabled Submit button is visually distinct (reduced
  opacity or grayed USS class) so the player understands they cannot submit yet.
- Activation: left mouse click, screen tap, or keyboard `Enter` key.
- On activation: call `BoardManager.SubmitWord()`. Do not play feedback
  immediately; wait for the result event.

**Clear Button**
- Always enabled during the `Playing` game state, regardless of selection
  length or animation state.
- Activation: left mouse click, screen tap, keyboard `Escape` key.
- On activation: call `BoardManager.ClearSelection()`.
- The Clear button does not consume a turn and does not interact with the
  reel advance logic; it only clears the selection.
- No feedback animation plays on Clear — the action is immediate and neutral.

**Keyboard Shortcuts**

| Key | Action | Condition |
|-----|--------|-----------|
| `Enter` | Submit | Submit button is enabled |
| `Escape` | Clear | Playing state |
| `Backspace` | Deselect rightmost selected reel | At least 1 reel selected |

The `Backspace` shortcut reduces `selectionEnd` by 1 (dispatched to Board
Manager as `SetSelection(selectionStart, selectionEnd - 1)`). If this would
make `selectionEnd < selectionStart`, it clears the selection entirely.

**Submission Feedback — Valid Word**
When `BoardManager.OnWordAccepted` fires:
1. Apply USS class `word-preview--valid` to the word preview element.
   This class adds a green background tint or text color for `FEEDBACK_VALID_DURATION`.
2. After `FEEDBACK_VALID_DURATION`, remove the class. The preview clears
   because the selection is also cleared by Board Manager on acceptance.
3. Do not play a shake animation on valid submission.

**Submission Feedback — Invalid Word**
When `BoardManager.OnWordRejected(RejectionReason reason)` fires:
1. Apply USS class `word-preview--invalid` to the word preview element.
   This class adds a red background tint or text color.
2. Simultaneously, apply USS class `word-preview--shake` which triggers
   a CSS keyframe animation (horizontal oscillation) defined in USS.
3. Both classes are removed after `FEEDBACK_INVALID_DURATION`.
4. The selection is NOT automatically cleared on invalid submission. The
   player may edit their selection and try again.
5. The Submit button re-enables immediately after the animation completes
   (it is disabled for `FEEDBACK_INVALID_DURATION` to prevent rapid
   re-submission during the animation).

**Rejection Reason Display (optional MVP stretch)**
If `SHOW_REJECTION_REASON` is enabled, a secondary small label below the
word preview shows a brief rejection hint:
- `NotAWord`: "Not in word list"
- `AlreadyUsed`: "Already used this game"
- `TooShort`: "Word too short" (edge case; Submit should already be disabled)

If `SHOW_REJECTION_REASON` is false (default), no secondary label is shown.

### States and Transitions

The Word Input UI component tracks an internal state that governs which
animations and button enable states are active:

```
[Idle]
    |-- selection length >= MIN_WORD_LENGTH --> Submit enabled
    |-- selection length < MIN_WORD_LENGTH  --> Submit disabled
    |-- Enter (when enabled)               --> [Submitting]
    |-- Escape / Clear click               --> stays [Idle], selection cleared

[Submitting]
    |-- OnWordAccepted fires               --> [FeedbackValid]
    |-- OnWordRejected fires               --> [FeedbackInvalid]
    (Submit button is disabled for the duration of any [Feedback*] state)

[FeedbackValid]
    |-- FEEDBACK_VALID_DURATION expires    --> [Idle]

[FeedbackInvalid]
    |-- FEEDBACK_INVALID_DURATION expires  --> [Idle]
    (player can still adjust selection during this state; they just can't re-submit)
```

The component is only interactive when Game State Machine is in `Playing`.
In all other states (`MainMenu`, `GameOver`, `Paused`), the component is
hidden or non-interactive.

### Interactions with Other Systems

**Board Manager (reads and dispatches)**
- Dispatches `BoardManager.SubmitWord()` on Submit activation.
- Dispatches `BoardManager.ClearSelection()` on Clear activation.
- Dispatches `BoardManager.SetSelection(start, end)` for Backspace shortcut.
- Subscribes to `BoardManager.OnWordAccepted` for valid feedback.
- Subscribes to `BoardManager.OnWordRejected(RejectionReason)` for invalid feedback.

**Board UI (reads)**
- Subscribes to Board UI's live word string update (pushed on every
  `OnSelectionChanged`) to refresh the word preview element.
- Queries `BoardUI.IsAnyReelAnimating()` to gate the Submit button.

**Game State Machine (responds)**
- Subscribes to state change events; disables all interaction when state
  is not `Playing`.

---

## Formulas

Word Input UI contains no game-logic formulas. Timing relationships:

**Feedback window duration**

```
submit_locked_duration = FEEDBACK_INVALID_DURATION
```

During `submit_locked_duration`, the Submit button remains disabled after
an invalid submission. This prevents double-submission spam during the
feedback animation.

| Variable | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `FEEDBACK_VALID_DURATION` | float (seconds) | 0.20–0.60 | 0.35 | Duration green highlight is visible |
| `FEEDBACK_INVALID_DURATION` | float (seconds) | 0.30–0.80 | 0.50 | Duration red highlight + shake are visible; also how long Submit is locked |
| `MIN_WORD_LENGTH` | int (chars) | 2–3 | 2 | Minimum selected reel count to enable Submit |

**Shake animation keyframes (defined in USS)**

The shake is a horizontal translate oscillation. Reference implementation:

```
t=0.00: translateX(0)
t=0.15: translateX(-SHAKE_MAGNITUDE)
t=0.35: translateX(+SHAKE_MAGNITUDE)
t=0.55: translateX(-SHAKE_MAGNITUDE * 0.5)
t=0.75: translateX(+SHAKE_MAGNITUDE * 0.5)
t=1.00: translateX(0)
```

Keyframe times are normalized (0–1) over the `FEEDBACK_INVALID_DURATION` window.

| Variable | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `SHAKE_MAGNITUDE` | float (px) | 4–12 | 8 | Peak horizontal displacement of the shake in CSS pixels |

---

## Edge Cases

**Submit fires while reel is animating (race condition)**
- `BoardUI.IsAnyReelAnimating()` is checked synchronously on every frame.
  If a reel enters `Animating` between the player pressing Enter and the
  event processing, the Submit button will be disabled on the next frame and
  the event is dropped. Board Manager also enforces this server-side: it
  ignores `SubmitWord()` calls while any reel is advancing.

**Player holds Enter key (key-repeat)**
- Unity UI Toolkit fires repeated key events on hold. Word Input UI debounces
  by ignoring Enter key events while in `Submitting` or `FeedbackValid` state.
  The button's disabled state provides the visual signal that the key is not
  having effect.

**Clear pressed during FeedbackInvalid**
- Clear is always enabled. Clearing during the invalid feedback animation
  immediately clears the selection and removes the feedback USS classes.
  The shake animation is stopped mid-play.

**Empty selection, Enter pressed**
- Submit button is disabled when selection is empty, so Enter produces no
  action. If somehow dispatched programmatically, Board Manager's
  `SubmitWord()` returns early with `RejectionReason.TooShort` and fires
  `OnWordRejected`, which triggers the invalid feedback. This is a safety
  net, not the primary enforcement.

**Backspace with single reel selected**
- `selectionEnd - 1 < selectionStart` triggers a full clear via
  `BoardManager.ClearSelection()` rather than `SetSelection`.

**Backspace with no reels selected**
- No action dispatched. Backspace is a no-op when selection is empty.

**Simultaneous Enter and Escape (unlikely but possible via automation)**
- Process in event queue order. Both are separate keyboard events. The first
  processed takes effect; the second is evaluated against the resulting state.

**OnWordAccepted fires with 0 reels advanced (future edge case)**
- This should not occur in current game logic, but if it does, the valid
  feedback still plays. Board UI handles the zero-reel animation case
  independently.

**Word preview overflows its container (very long word)**
- With 6 reels the maximum word length is 6 characters. At any reasonable
  font size this will not overflow. No truncation logic is needed for MVP.
  If post-MVP adds more reels, apply USS `overflow: hidden` and
  `text-overflow: ellipsis` as a safety measure.

---

## Dependencies

| System | Direction | Contract |
|--------|-----------|----------|
| Board Manager | Word Input UI dispatches to | Provides `SubmitWord()`, `ClearSelection()`, `SetSelection(start, end)`, events `OnWordAccepted`, `OnWordRejected(RejectionReason)` |
| Board UI | Word Input UI reads | Provides live word string (pushed on selection change), `IsAnyReelAnimating()` |
| Game State Machine | Word Input UI responds | Disables all interaction outside `Playing` state |

**What this system provides to others:**
- No other system reads from Word Input UI. It is a terminal presentation node.

**What this system requires from others:**
- Board Manager: submission outcome events and action endpoints
- Board UI: live word string and animation query
- Game State Machine: state change events to enable/disable the component

---

## Tuning Knobs

All values live in `assets/data/ui-config.json` under the `word_input_ui` key.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `FEEDBACK_VALID_DURATION` | Feel | 0.35s | 0.20–0.60s | How long the green "accepted" highlight is visible. Too short = missed; too long = delays next action. |
| `FEEDBACK_INVALID_DURATION` | Feel | 0.50s | 0.30–0.80s | How long the red/shake feedback plays AND how long Submit is locked after rejection. |
| `SHAKE_MAGNITUDE` | Feel | 8px | 4–12px | Peak displacement of the shake animation. Below 4px is imperceptible; above 12px feels violent. |
| `MIN_WORD_LENGTH` | Gate | 2 | 2–3 | Minimum reels selected to enable Submit. 2 = permissive; 3 = requires at least a 3-letter word. |
| `WORD_PREVIEW_PLACEHOLDER_TEXT` | Feel | `"_ _ _ _ _ _"` | any string | Shown when no reels are selected. Empty string is also valid. |
| `SHOW_REJECTION_REASON` | Gate | false | bool | Whether to display a secondary rejection reason label. Disabled by default for MVP to reduce UI clutter. |

---

## Acceptance Criteria

### Functional Criteria (QA-testable)

1. **Live preview updates**: Selecting reel 0 and reel 1 immediately updates
   the word preview to show the two-character string. Deselecting reel 1
   immediately shows the one-character string.

2. **Submit disabled below minimum length**: With zero or one reel selected
   (default `MIN_WORD_LENGTH = 2`), the Submit button's USS disabled class
   is applied and clicking it or pressing Enter has no effect.

3. **Submit enabled at minimum length**: With two or more reels selected and
   no reel animating, the Submit button's USS disabled class is absent and
   Enter or click triggers `BoardManager.SubmitWord()`.

4. **Valid feedback timing**: After `OnWordAccepted`, the `word-preview--valid`
   USS class is present for `FEEDBACK_VALID_DURATION` seconds (±1 frame at 60fps),
   then removed.

5. **Invalid feedback timing**: After `OnWordRejected`, both
   `word-preview--invalid` and `word-preview--shake` USS classes are present
   for `FEEDBACK_INVALID_DURATION` seconds, then removed.

6. **Submit locked during invalid feedback**: After `OnWordRejected`, pressing
   Enter or clicking Submit does not call `BoardManager.SubmitWord()` until
   `FEEDBACK_INVALID_DURATION` has elapsed.

7. **Clear always works**: With any selection state (including during
   `FeedbackInvalid`), pressing Escape or clicking Clear calls
   `BoardManager.ClearSelection()` and removes any active feedback USS classes.

8. **Backspace removes last letter**: With reels 1-3 selected, pressing
   Backspace results in reels 1-2 selected and the preview showing only 2 chars.

9. **Backspace clears single selection**: With only reel 3 selected, pressing
   Backspace results in no reels selected and the preview showing placeholder.

10. **Submit blocked during animation**: While `BoardUI.IsAnyReelAnimating()`
    returns true, the Submit button is visually disabled and Enter has no effect.

11. **UI Toolkit only**: Inspector confirms no `Canvas`, `InputField` (UGUI),
    or `Button` (UGUI) components on any Word Input UI game object.

### Experiential Criteria (Playtest)

12. **Invalid feedback is unambiguous**: Every playtester immediately
    understands a submission was rejected (not just ignored) without being
    told what the feedback means. Target: 5 of 5 playtesters identify
    the invalid feedback correctly on first occurrence.

13. **Clear is discoverable**: Within the first 3 minutes of play, at least
    4 of 5 playtesters use the Clear function without prompting (either
    button or keyboard), indicating it is legible and accessible.

14. **No input frustration from Submit gating**: Playtesters do not express
    frustration about the Submit button being disabled. They understand intuitively
    why it is disabled. Target: 0 of 5 playtesters mention confusion about
    Submit availability in a post-session debrief.

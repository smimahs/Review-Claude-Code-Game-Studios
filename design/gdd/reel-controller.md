# Reel Controller

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic Clarity — board state always legible; player knows exactly what will happen

---

## Overview

The Reel Controller manages a single reel in the ReelWords board. Each of the six reels has exactly one Reel Controller instance. The controller holds a reference to its immutable character sequence (supplied by the Reel Sequence Data asset), tracks the current position within that sequence as an integer index, and exposes a `CurrentChar` property and an `Advance()` method. The controller also maintains a simple three-state machine (Idle, Selected, Advancing) that drives visual feedback without coupling presentation logic into the controller itself. The Reel Controller has no knowledge of words, scoring, or other reels — it is a single-responsibility component that answers two questions: "what character am I showing?" and "move forward one step."

---

## Player Fantasy

The player should feel that each reel is a tangible, mechanical object — like a physical slot machine drum they can predict and control. When a word is submitted and reels advance, the motion must feel earned and purposeful: the player chose those reels, the player knew what character would come next, and the advance is the satisfying confirmation of a plan executed. The determinism of the fixed sequence is the design's core promise. A skilled player who has studied the board should feel like an expert who can see several moves ahead, not someone at the mercy of randomness.

Primary MDA aesthetics served: **Challenge** (planning and optimization against a known system) and **Discovery** (learning each reel's sequence as mastery knowledge).

---

## Detailed Design

### Core Rules

1. Each Reel Controller owns exactly one character sequence: an ordered, immutable list of characters defined in the Reel Sequence Data asset. The sequence is assigned at initialization and never changes during a session.
2. The controller tracks `_currentIndex`, an integer initialized to 0 at game start.
3. `CurrentChar` always returns the character at `_currentIndex % sequence.Length`. The modulo operation means the sequence is treated as a circular buffer.
4. `Advance()` increments `_currentIndex` by 1. There is no upper bound clamp — the modulo in `CurrentChar` handles wrap-around transparently. This means `_currentIndex` always increases monotonically and never resets during a session (unless the game is restarted). This design choice preserves the ability to query how many times a reel has advanced (useful for analytics and potential future features).
5. `Advance()` is called exclusively by the Board Manager. The Reel Controller does not call `Advance()` on itself.
6. Reels do not advance on invalid word submissions. The Board Manager is responsible for this gate; the Reel Controller does not inspect submission validity.
7. A reel that is not part of the selected contiguous subset for the current word is not advanced and does not change state.

### States and Transitions

The Reel Controller maintains an internal `ReelState` enum used by the Board UI to drive visual presentation.

```
ReelState:
  Idle        — default state; reel is not part of current selection
  Selected    — reel is part of the player's current word selection (highlighted)
  Advancing   — reel has just been advanced; animating to new character (brief transitional state)
```

**Transition rules:**

| From | To | Trigger |
|------|----|---------|
| Idle | Selected | Board Manager includes this reel in current selection |
| Selected | Idle | Board Manager clears current selection (submit or clear) |
| Selected | Advancing | Board Manager calls `Advance()` on this reel (valid submission only) |
| Advancing | Idle | Advance animation completes (animation duration is a tuning knob) |

The `Advancing` state is purely presentational. The character index is updated immediately when `Advance()` is called; the `Advancing` state signals the Board UI that it should play the reel-advance animation. Logic systems (Word Validator, Scoring) operate on the new `CurrentChar` value immediately — they do not wait for the animation to complete.

The controller fires a `OnStateChanged(ReelState newState)` event whenever state changes. The Board UI subscribes to this event. No other system needs to subscribe.

### Interactions with Other Systems

- **Reel Sequence Data**: Provides the character array at initialization. The controller takes the array by reference at startup; it does not poll the data asset after initialization.
- **Board Manager**: The only system that calls `Advance()`. The Board Manager also drives state transitions by calling `SetState(ReelState)` on the controller. The controller does not pull state from the Board Manager — the Board Manager pushes state to the controller.
- **Board UI**: Subscribes to `OnStateChanged` to trigger animations and update the displayed character glyph. The UI reads `CurrentChar` to determine what to render.
- **Word Validator**: Reads `CurrentChar` directly from the controller as part of building the candidate word string. The validator does not call any other methods on the controller.

---

## Formulas

### Current Character Formula

```
CurrentChar = sequence[_currentIndex % sequence.Length]
```

**Variables:**
- `sequence`: `char[]` — the fixed character array for this reel, length N (1 ≤ N ≤ 26, practical range 6–20)
- `_currentIndex`: `int` — monotonically increasing advance counter, initialized to 0, range [0, ∞)
- `sequence.Length`: `int` — number of characters in this reel's sequence, constant per session

**Example (sequence = ['A','P','P','L','E'], length = 5):**

| `_currentIndex` | `_currentIndex % 5` | `CurrentChar` |
|----------------|---------------------|---------------|
| 0 | 0 | 'A' |
| 1 | 1 | 'P' |
| 2 | 2 | 'P' |
| 3 | 3 | 'L' |
| 4 | 4 | 'E' |
| 5 | 0 | 'A' (wrapped) |
| 6 | 1 | 'P' |

**Wrap behavior:** After `sequence.Length` advances, the reel returns to its first character. This is transparent to all consuming systems — they only read `CurrentChar`, which always returns a valid character.

### Advance Count Formula

```
AdvanceCount = _currentIndex
CyclesCompleted = _currentIndex / sequence.Length   (integer division)
PositionInCurrentCycle = _currentIndex % sequence.Length
```

`AdvanceCount` is exposed as a read-only property for analytics purposes. It is not used by any core game system.

---

## Edge Cases

**Empty sequence (length 0):** Must not occur. The Reel Sequence Data validation step (performed at asset load time by the Board Manager) must assert that every reel's sequence has at least one character. If somehow reached, `sequence[_currentIndex % 0]` would produce a divide-by-zero exception. The controller should guard with an assertion in debug builds: `Debug.Assert(sequence.Length > 0, "Reel sequence must not be empty")`.

**Single-character sequence (length 1):** Valid and intentional. `CurrentChar` always returns the same character. The reel advances (index increments) but the visible character never changes. This is a legal design choice for a reel that is intentionally fixed. No special handling required beyond the standard formula.

**Maximum index overflow (`_currentIndex` reaching `int.MaxValue`):** At the maximum turn count of 100 turns per session (see Turn Manager), and with a maximum of 6 reels advancing per turn, the theoretical maximum advances per session is 600. `int.MaxValue` is 2,147,483,647. Integer overflow is not a practical concern for this game. No overflow protection is required.

**`Advance()` called during `Advancing` state:** This should not occur under normal gameplay (Board Manager waits for animation completion before accepting the next submission). If it does occur (e.g., in a unit test or animation is skipped), the behavior is defined: `_currentIndex` increments again immediately, `CurrentChar` updates, and `OnStateChanged` fires. The animation system handles interrupted animations via standard Unity animation interruption rules.

**`SetState()` called with the current state:** The controller should early-exit without firing `OnStateChanged`. Firing a state-change event for a no-op transition would cause the Board UI to trigger unnecessary animation updates.

**Reel not included in any valid word for the entire session:** The reel's `_currentIndex` remains 0 throughout the session. `CurrentChar` always returns `sequence[0]`. This is fully valid behavior — some reels may never advance in a given session depending on player strategy.

---

## Dependencies

### What This System Requires

| Dependency | Type | Contract |
|-----------|------|---------|
| Reel Sequence Data | Data Asset | Provides `char[]` sequence at initialization. Must be non-null, non-empty. Injected by Board Manager during scene setup. |
| Board Manager | Runtime Caller | Calls `Advance()` and `SetState(ReelState)`. Reel Controller does not hold a reference back to Board Manager. |

### What This System Provides

| Consumer | Provided Interface |
|---------|-------------------|
| Board Manager | `void Advance()` — increments internal index |
| Board Manager | `void SetState(ReelState state)` — drives state machine |
| Board UI | `char CurrentChar { get; }` — current character for rendering |
| Board UI | `event OnStateChanged(ReelState)` — animation/highlight trigger |
| Word Validator | `char CurrentChar { get; }` — character contributed to candidate word |
| Analytics (future) | `int AdvanceCount { get; }` — total number of advances this session |

### Bidirectional Dependency Notes

- **Board Manager** depends on Reel Controller (calls `Advance()`, reads `CurrentChar`). The Board Manager GDD must list Reel Controller as a dependency.
- **Board UI** depends on Reel Controller (subscribes to `OnStateChanged`, reads `CurrentChar`). The Board UI GDD must list Reel Controller as a dependency.
- **Word Validator** depends on Reel Controller (reads `CurrentChar`). The Word Validator GDD lists Reel Controller as an indirect dependency via Board Manager injection.

---

## Tuning Knobs

All tuning values live in `assets/data/reel-config.json`. No values are hardcoded.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|-----------|--------|
| `advanceAnimationDuration` | Feel | 0.25s | 0.1s – 0.6s | Duration of the reel-advance spin animation. Lower = snappier, higher = more theatrical. Below 0.1s the animation reads as a glitch rather than a satisfying click. Above 0.6s the pacing of rapid word submission becomes frustrating. |
| `selectedHighlightDuration` | Feel | Instant | — | How quickly the Selected highlight appears. Currently instant (0s). Reserved as a knob if playtesting reveals the snap feels abrupt. |
| `sequenceLength` (per reel) | Curve | 8 | 4 – 16 | The number of characters in each reel's sequence. Shorter sequences cycle faster (more dynamic board, potentially more planned combos). Longer sequences feel more stable (easier to plan ahead). This is a design-time authoring decision, not a runtime parameter. |

---

## Acceptance Criteria

### Functional Criteria

- [ ] `CurrentChar` returns `sequence[0]` when `_currentIndex` is 0 (initial state)
- [ ] After one call to `Advance()`, `CurrentChar` returns `sequence[1]`
- [ ] After `sequence.Length` calls to `Advance()`, `CurrentChar` returns `sequence[0]` (wrap confirmed)
- [ ] `_currentIndex` increments monotonically; it never decreases during a session
- [ ] `OnStateChanged` fires exactly once per state transition, not on repeated `SetState()` with the same state
- [ ] `CurrentChar` returns the correct character immediately after `Advance()` is called, before any animation completes
- [ ] A reel initialized with a single-character sequence always returns that character and does not throw on repeated `Advance()` calls
- [ ] Debug build assertion fires if sequence length is 0

### Experiential Criteria (Playtesting)

- [ ] The advance animation reads clearly as "this reel moved forward" — distinct from idle and selected states
- [ ] Players can correctly predict `CurrentChar` after an advance before the animation completes (i.e., the new character is visible at animation start, not revealed at animation end)
- [ ] After 5 minutes of play, players can articulate the wrap-around behavior without being told — the sequence cycling should be discoverable through play
- [ ] No player reports the reel advance as feeling "random" — determinism must be perceptible

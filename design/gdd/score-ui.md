# Score UI

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Satisfying Progression — every word found visibly changes the board

---

## Overview

The Score UI displays the player's session running total score and delivers
a word-score flyout animation each time a valid word is submitted. The session
total is a large, always-visible integer label positioned prominently in the
HUD. When a valid word is accepted, a transient "+N pts" label animates upward
from the reel area and fades out over 1–2 seconds, giving the player an
immediate and spatially grounded reward signal before the total updates.
The Score UI reads from the Scoring System only — it never writes to it — and
all rendering uses Unity 6 UI Toolkit (UIDocument, UXML, USS).

---

## Player Fantasy

Every word submission should feel like a win event, not just a number changing.
The flyout popup transforms an abstract integer increment into a physical reward
that emerges from the action site (the reels) and rises like a score bubble in
a pinball machine. The player should feel their score growing as a stream of
satisfying positive moments, not as a counter they check at the end. The session
total, always visible and large, creates a sense of momentum — even a small
word makes it tick upward in a way the player can see.

The primary MDA aesthetic this mechanic serves:
- **Sensation**: the animated flyout creates a visceral reward pulse at the
  exact moment of word acceptance.
- **Challenge**: the visible score anchors the player's assessment of whether
  their word choices are paying off, satisfying the Competence need from
  Self-Determination Theory.

---

## Detailed Design

### Core Rules

**Session Total Display**
- One persistent label element shows the session running total as an integer
  with no decimal places.
- Initial value at game start: `0`.
- The label updates to the new total immediately when
  `ScoringSystem.OnScoreUpdated(int newTotal)` fires.
- No counting-up animation on the session total label during play (only on
  the Game Over screen). Updates are instantaneous to keep feedback crisp.
- Format: plain integer string. No currency symbol, no padding.
  Examples: `0`, `14`, `327`, `1084`.

**Word Score Flyout**
When `ScoringSystem.OnScoreChanged(int sessionScore, int wordScore)` fires with `wordScore > 0`:
1. Query flyout spawn position independently: call `BoardUI.GetReelScreenRect(index)` for the first and last reel in the most recently advanced set (obtained by also subscribing to `BoardManager.OnWordAccepted(int[] advancedReelIndices)`). Compute the horizontal center of the combined rect and the vertical top edge of the reel windows. The Scoring System carries no screen coordinates — position is the Score UI's responsibility.
2. Instantiate (or pool) a flyout label element at the computed spawn position.
3. Set the flyout label text to `"+" + wordScore + " pts"`.
   Example: `"+14 pts"`.
4. Animate the flyout:
   - **Phase 1 (Rise)**: Over `FLYOUT_RISE_DURATION`, translate the label
     upward by `FLYOUT_RISE_DISTANCE` pixels using a USS animation or
     UI Toolkit `ITransitionAnimatable`.
   - **Phase 2 (Fade)**: Beginning at `FLYOUT_FADE_START_RATIO * FLYOUT_TOTAL_DURATION`
     into the animation, linearly reduce opacity from 1.0 to 0.0.
   - At `FLYOUT_TOTAL_DURATION`, the animation completes. Remove or return
     the element to pool.
4. Multiple flyouts may be active simultaneously if the player submits words
   in very rapid succession (timer mode). Each flyout is independent.

**Flyout Spawn Position**
Score UI computes its own spawn position — no screen coordinates come from gameplay systems:
1. Score UI subscribes to `BoardManager.OnWordAccepted(int[] advancedReelIndices)`.
2. On that event, Score UI calls `BoardUI.GetReelScreenRect(first)` and `BoardUI.GetReelScreenRect(last)` where `first` and `last` are the first and last indices in `advancedReelIndices`.
3. Spawn X = horizontal center of the combined rects. Spawn Y = top edge of the reel windows.
4. The flyout rises upward from the spawn point (screen Y decreases).

**Session Total Position**
The session total is always visible in the HUD. Its exact screen position is
defined by the UXML layout (top-right area of the game screen, or above the
reel area — exact placement is an art/UX decision, not locked by this document).
What is locked: it must be on-screen and legible during the entire `Playing`
state, including during flyout animations.

### States and Transitions

The Score UI has two concurrent elements with independent lifecycles:

**Session Total Label** — persistent, single instance:
```
[Showing 0] -- OnScoreUpdated(N) --> [Showing N] (immediate, no animation)
```

**Flyout Instance** — spawned per word, pooled:
```
[Pooled]
    |-- OnWordScored fires  --> [Rising + visible]
    |-- Rise phase ends     --> [Fading]
    |-- Fade phase ends     --> [Pooled]
```

### Interactions with Other Systems

**Scoring System (reads)**
- Subscribes to `ScoringSystem.OnScoreChanged(int sessionScore, int wordScore)` for both the session total update and the flyout trigger. When `wordScore > 0`, a flyout is spawned.

**Board Manager (reads, for spawn position)**
- Subscribes to `BoardManager.OnWordAccepted(int[] advancedReelIndices)` to know which reels advanced (used to compute flyout spawn position).

**Board UI (reads, for spawn position)**
- Calls `BoardUI.GetReelScreenRect(int reelIndex)` to compute the flyout spawn position from the advanced reel indices. This is a read-only query; Board UI is not modified.

**Game State Machine (responds)**
- Resets session total label to `0` on `OnGameStarted`.
- Hides Score UI (or shows `0`) on `MainMenu` and `GameOver` states.
  (Game Over Screen has its own final score display; Score UI is hidden
  during that state to avoid visual duplication.)

---

## Formulas

**Flyout timing**

```
FLYOUT_TOTAL_DURATION = FLYOUT_RISE_DURATION + FLYOUT_HOLD_DURATION
fade_start_time = FLYOUT_FADE_START_RATIO * FLYOUT_TOTAL_DURATION
```

| Variable | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `FLYOUT_RISE_DURATION` | float (seconds) | 0.50–1.50 | 0.90 | Duration of the upward translation phase |
| `FLYOUT_HOLD_DURATION` | float (seconds) | 0.00–0.50 | 0.10 | Optional hold at peak before fade (set 0 to skip) |
| `FLYOUT_TOTAL_DURATION` | float (seconds) | derived | 1.00 | Total flyout lifetime |
| `FLYOUT_FADE_START_RATIO` | float (0–1) | 0.40–0.80 | 0.60 | Fraction of total duration at which opacity begins fading |
| `FLYOUT_RISE_DISTANCE` | float (px) | 40–120 | 80 | Total upward travel in screen pixels |
| `FLYOUT_MAX_CONCURRENT` | int | 2–8 | 4 | Maximum simultaneous flyout instances before pooling reuse |

Example with defaults:
- Flyout spawns at reel midpoint.
- Rises 80px over 0.90s.
- Holds at peak for 0.10s.
- Begins fading at `0.60 * 1.00 = 0.60s`.
- Fully transparent and removed at 1.00s.

**Word score text format**

```
flyout_text = "+" + wordScore.ToString() + " pts"
```

No formula: `wordScore` is an integer from the Scoring System. The display
is a direct string interpolation. There are no decimals, rounding, or
multiplier formatting in MVP.

---

## Edge Cases

**Score of 0 (invalid sequence of letters submitted that passes validation — impossible in current rules)**
- `OnWordScored(0, ...)` would produce "+0 pts" flyout. This should not occur
  because every letter has a value >= 1 (minimum Scrabble value). No special
  handling needed, but if encountered, the flyout displays normally.

**Very high word score (theoretical maximum: 6 letters, all high-value)**
- Maximum possible score: 6 × Q (10 pts) = 60 pts. Flyout text: `"+60 pts"`.
  This is a short string; no truncation needed.

**Session total exceeds 4 digits**
- A session of N turns each scoring ~10 pts could reach 4-digit totals. The
  session total label must accommodate at least 4 digits without layout
  breaking. Test at `9999` and `10000` to verify the UXML layout flexes
  correctly (use `min-width` rather than fixed width in USS).

**Multiple flyouts simultaneously (timer mode)**
- Up to `FLYOUT_MAX_CONCURRENT` flyouts can be alive at once. If a new word
  is scored when the pool is exhausted (all `FLYOUT_MAX_CONCURRENT` instances
  are active), reuse the oldest active flyout: reset its position, text,
  and restart its animation. This is a graceful degradation; it should be
  imperceptible at normal play speed.

**Flyout spawn at screen edge (reel 0 or reel 5 selected alone)**
- The spawn position may be near the left or right edge of the screen. The
  flyout rises vertically; horizontal position is fixed at spawn. No clamping
  is needed because the reels are always within the game area. Verify in
  layout that reels 0 and 5 are inset enough from screen edges that the
  flyout text does not clip.

**Game Over fires while flyout is mid-animation**
- Flyouts are immediately destroyed (returned to pool) when `OnGameOver`
  fires. The final score on the Game Over Screen is authoritative; any
  in-flight flyout is cosmetic and can be discarded.

**Score resets between sessions**
- `OnGameStarted` resets the session total label to `0`. Any in-flight
  flyouts are cleared. The Score UI must handle rapid session cycling
  (restart immediately) without leaked flyout instances.

---

## Dependencies

| System | Direction | Contract |
|--------|-----------|----------|
| Scoring System | Score UI reads | Provides `OnScoreChanged(int sessionScore, int wordScore)` — single event for both total update and flyout trigger |
| Board Manager | Score UI reads | Provides `OnWordAccepted(int[] advancedReelIndices)` — used to determine flyout spawn reels |
| Board UI | Score UI reads | Provides `GetReelScreenRect(int reelIndex)` to compute flyout spawn position |
| Game State Machine | Score UI responds | `OnGameStarted` resets score to 0; state changes hide/show Score UI |

**What this system provides to others:**
- No other system reads from Score UI. It is a terminal presentation node.

**What this system requires from others:**
- Scoring System: score update and word-scored events
- Board UI: reel screen position query for flyout spawning
- Game State Machine: state change events

---

## Tuning Knobs

All values live in `assets/data/ui-config.json` under the `score_ui` key.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `FLYOUT_RISE_DURATION` | Feel | 0.90s | 0.50–1.50s | How long the flyout travels upward. Shorter = snappy; longer = floaty. |
| `FLYOUT_HOLD_DURATION` | Feel | 0.10s | 0.00–0.50s | Pause at peak before fade begins. Set to 0 to go straight to fade. |
| `FLYOUT_FADE_START_RATIO` | Feel | 0.60 | 0.40–0.80 | Fraction of total duration before opacity begins dropping. |
| `FLYOUT_RISE_DISTANCE` | Feel | 80px | 40–120px | How far the flyout travels vertically before disappearing. |
| `FLYOUT_MAX_CONCURRENT` | Gate | 4 | 2–8 | Maximum simultaneous flyout instances. Increase for timer mode if players score rapidly. |

---

## Acceptance Criteria

### Functional Criteria (QA-testable)

1. **Score initializes to 0**: At game start (`OnGameStarted`), the session
   total label displays exactly `"0"`.

2. **Score updates immediately**: After `OnScoreUpdated(42)`, the session
   total label displays `"42"` within 1 frame (no animation delay on the total).

3. **Flyout text correct**: After `OnWordScored(14, ...)`, the flyout label
   text is exactly `"+14 pts"`.

4. **Flyout rises**: The flyout element's Y position (screen space) decreases
   by at least `FLYOUT_RISE_DISTANCE * 0.90` pixels over `FLYOUT_RISE_DURATION`
   (±10% tolerance for easing).

5. **Flyout fades**: At `fade_start_time`, the flyout opacity is 1.0. At
   `FLYOUT_TOTAL_DURATION`, the opacity is 0.0 (±0.05 tolerance).

6. **Flyout removed after duration**: At `FLYOUT_TOTAL_DURATION + 0.10s`,
   no flyout element with that word's text is present in the UIDocument tree.

7. **Multiple concurrent flyouts**: Scoring two words within 0.30s of each
   other results in two simultaneously rising flyout elements with independent
   positions and correct point values.

8. **Pool reuse on exhaustion**: Scoring `FLYOUT_MAX_CONCURRENT + 1` words
   in rapid succession (automated test) does not throw an exception or create
   more than `FLYOUT_MAX_CONCURRENT` flyout elements simultaneously.

9. **Score resets on restart**: Completing a game, then starting a new game,
   results in the session total label showing `"0"` at the start of the new
   game, not the previous session's final score.

10. **Score hidden during Game Over**: When Game State Machine enters
    `GameOver`, the Score UI session total label is no longer visible (or
    the Score UI root element has `display: none`).

11. **UI Toolkit only**: Inspector confirms no `Canvas`, `Text` (UGUI), or
    `Image` (UGUI) components on Score UI game objects.

### Experiential Criteria (Playtest)

12. **Flyout is noticed**: At least 4 of 5 playtesters spontaneously mention
    or react to the word score popup during a session, without being asked
    about it, indicating it is visible and salient.

13. **Score feels rewarding**: At least 4 of 5 playtesters describe the score
    feedback as "satisfying," "rewarding," or "clear" in a post-session debrief.
    Target: 0 playtesters describe it as "I didn't notice the score" or "I
    couldn't tell how well I was doing."

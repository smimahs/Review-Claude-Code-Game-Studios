# Board Manager

> Status: Designed | Author: Claude Code Game Studios | Last Updated: 2026-03-26

---

## Overview

The Board Manager is the central coordinator of ReelWords' core gameplay loop. It owns the array of six ReelController instances and tracks the player's current reel selection (which reels are included in the word being composed). When the player submits a word, the Board Manager executes the complete submission pipeline in strict order: it assembles the word string from selected reel characters, calls Word Validator, fires a submission-attempted event, optionally scores the word and advances the used reels, clears the selection, and fires a completion event. Every other feature system — Turn Manager, Timer System, Game Mode Manager, Board UI, and Word Input UI — reacts to Board Manager events rather than calling into it directly during the submission flow, making Board Manager the authoritative source of board truth.

---

## Player Fantasy

The player should feel that the board is a legible, responsive instrument under their full control. When they select reels and submit a word, they experience a clean cause-and-effect chain: letters lock in, validation fires, and the specific reels they used visibly advance while the others stay frozen. There is no ambiguity about what just happened or why. This serves the **Strategic Clarity** design pillar — the player always knows exactly what the board state is and exactly what their word submission will change.

MDA Aesthetics primarily served: **Challenge** (optimization decisions under turn constraints) and **Discovery** (finding valid words within the current board configuration).

---

## Detailed Design

### Core Rules

1. The Board Manager owns a fixed-size array of exactly 6 ReelController references, indexed 0 through 5 (left to right). This array is populated at scene load and never changes during a session.

2. The player interacts with the board through two operations: toggling reel selection and submitting the current selection. No other direct board mutations are permitted from outside the Board Manager.

3. **Selection rules — contiguity constraint:**
   - Only contiguous reel subsets are valid selections: [0], [1], [2], [3], [4], [5], [0,1], [1,2], [2,3], [3,4], [4,5], [0,1,2], etc.
   - A gap in the selection (e.g. reels 0 and 2 with reel 1 not selected) is not permitted.
   - Minimum selection size: 1 reel (single-character "words" are accepted for submission; the Word Validator determines whether they are valid words).
   - Maximum selection size: 6 reels (all reels selected).
   - Selection is always represented as a contiguous range described by `_selectionStart` and `_selectionEnd` (inclusive integer indices). Derived property `SelectedReelIndices` returns the expanded list.

4. **Selection interaction model (left-to-right extension):**
   - When no reels are selected (Idle state), clicking any reel sets both `_selectionStart` and `_selectionEnd` to that reel's index and transitions to Selecting state.
   - When in Selecting state, clicking a reel adjacent to either end of the current selection extends the selection to include that reel. Clicking the reel at `_selectionStart` when it equals `_selectionEnd` (single reel selected) clears the selection and returns to Idle.
   - Clicking a reel that is already inside the current selection (but not at the ends) has no effect.
   - Clicking a reel that is not adjacent to the current selection has no effect (no gap-creation).
   - Clicking the reel at `_selectionStart` (when `_selectionEnd > _selectionStart`) removes it from the selection, advancing `_selectionStart` by 1.
   - Clicking the reel at `_selectionEnd` removes it from the selection, reducing `_selectionEnd` by 1.
   - If `_selectionStart > _selectionEnd` after a removal, the selection is empty and state returns to Idle.

5. **Word string assembly:** The word string is formed by reading `ReelController.CurrentChar` from each selected reel in ascending index order and concatenating them. This string is passed to Word Validator as-is (the Validator is responsible for case normalization).

### States and Transitions

The Board Manager operates in one of three internal states:

| State | Description | Valid Inputs |
|-------|-------------|--------------|
| `Idle` | No reels selected; waiting for player to start a selection | Reel click (starts selection) |
| `Selecting` | One or more contiguous reels are selected; word being composed | Reel click (extend/shrink selection), Submit, Clear |
| `Submitting` | Word submission is in progress; input is locked | None (system-driven) |

**Transition table:**

| From | To | Trigger |
|------|----|---------|
| `Idle` | `Selecting` | Player clicks any reel |
| `Selecting` | `Idle` | Player clicks Clear, or last selected reel is deselected |
| `Selecting` | `Submitting` | Player triggers Submit |
| `Submitting` | `Idle` | Submission pipeline completes (success or failure) |

The Board Manager does not expose a public method for external systems to force a state transition. State changes are internal and event-driven.

### Submission Pipeline (Ordered Steps)

The following steps execute synchronously in strict order when the player triggers Submit while in Selecting state. No step may be skipped or reordered.

**Step 1 — Guard check.** If not in `Selecting` state, ignore the Submit input and return immediately. Log a warning.

**Step 2 — Assemble word string.** Read `CurrentChar` from each reel in `SelectedReelIndices` (ascending order). Concatenate to form `submittedWord` (string).

**Step 3 — Validate.** Call `WordValidator.Validate(submittedWord)`. Store result as `isValid` (bool).

**Step 4 — Fire `OnSubmissionAttempted`.** Fire `OnSubmissionAttempted(string word, bool isValid)` event. This event is fired regardless of whether the word is valid. Turn Manager listens to this event to consume a turn.

**Step 5 — Branch on validity:**
- **If valid:**
  - Call `ScoringSystem.ScoreWord(submittedWord)`. Store result as `wordScore` (int).
  - Call `Advance()` on each ReelController in `SelectedReelIndices`.
  - Capture `advancedReelIndices` as a copy of the current `SelectedReelIndices` list.
- **If invalid:**
  - No scoring call. No reel advancement. `wordScore` = 0. `advancedReelIndices` = empty list.

**Step 6 — Clear selection.** Set `_selectionStart` and `_selectionEnd` to sentinel values (e.g. -1). Transition state to `Idle`.

**Step 7 — Fire completion event (valid only).** If the word was valid, fire `OnWordSubmitted(string word, int score, List<int> advancedReelIndices)`.

**Step 8 — Complete.** Transition from `Submitting` to `Idle` is considered complete after Step 7 (or after Step 6 if invalid). The Board Manager is now ready for the next player input.

Note: Turn Manager reacts to `OnSubmissionAttempted` (Step 4), which fires before the reel advancement (Step 5). This is intentional — the turn is consumed at the moment of submission commitment, not after the board resolves.

### Interactions with Other Systems

**ReelController (owns 6 instances):** Board Manager calls `CurrentChar` (read) and `Advance()` (write) on each controller. Board Manager never calls any other ReelController method.

**WordValidator (calls out):** Board Manager calls `WordValidator.Validate(word)` synchronously during submission. It does not cache results.

**ScoringSystem (calls out):** Board Manager calls `ScoringSystem.ScoreWord(word)` synchronously, but only on valid submissions. It passes the raw word string and receives an integer score.

**TurnManager (listens in):** Turn Manager subscribes to `BoardManager.OnSubmissionAttempted`. Board Manager has no reference to Turn Manager.

**TimerSystem (listens in):** Timer System may subscribe to `BoardManager.OnWordSubmitted` to apply bonus time on valid submissions. Board Manager has no reference to Timer System.

**Board UI (listens in):** Board UI subscribes to `OnSelectionChanged`, `OnWordSubmitted`, and `OnSubmissionAttempted` to update visual state. Board Manager has no reference to Board UI.

**Word Input UI (listens in):** Word Input UI subscribes to `OnSelectionChanged` to reflect the current word being composed. Board Manager has no reference to Word Input UI.

---

## Formulas

### Current Word String

```
W = CONCAT(Reel[i].CurrentChar) for each i in SelectedReelIndices, ascending order
```

Example: Reels 1, 2, 3 are selected with CurrentChars 'C', 'A', 'T'. W = "CAT".

### Word Length

```
L = _selectionEnd - _selectionStart + 1
  where _selectionStart, _selectionEnd ∈ [0, 5]
  range: L ∈ [1, 6]
```

### Contiguity Validation

A proposed selection set S is contiguous if and only if:

```
S = { _selectionStart, _selectionStart + 1, ..., _selectionEnd }
  where _selectionStart <= _selectionEnd
```

Any proposed selection that cannot be expressed as this range is rejected without state change.

### Advance Count Per Submission

```
AdvancedCount = |SelectedReelIndices|   (if word is valid)
AdvancedCount = 0                       (if word is invalid)
  range: AdvancedCount ∈ [0, 6]
```

---

## Edge Cases

**Submitting with no reels selected:** The guard check in Step 1 prevents this. If Submit is called while in Idle state, it is silently ignored. A log warning is emitted for debugging purposes.

**All 6 reels selected and word is invalid:** All steps proceed normally. No reels advance. The turn is still consumed (Turn Manager receives `OnSubmissionAttempted`). Board returns to Idle.

**All 6 reels selected and word is valid:** All 6 ReelControllers receive `Advance()`. This is a legal state and the maximum possible board change per turn. No special handling required.

**ReelController.Advance() on the last character in its sequence:** This is handled entirely by ReelController (it wraps to index 0). Board Manager does not need to know the reel's sequence length.

**Submission triggered while already in Submitting state:** The guard check in Step 1 prevents double-submission. Ignored silently. Log warning emitted.

**Word consisting of a single character:** L = 1 is permitted. The word is passed to WordValidator as normal. Single-letter valid words (e.g. "A", "I") are possible if the validator's dictionary includes them. This is a valid play — one reel advances.

**WordValidator returns an exception or error:** Board Manager treats any non-valid result as invalid. It fires `OnSubmissionAttempted(word, false)`. The submission pipeline continues normally to Steps 6 and 8 (no scoring, no advancement). The error is logged.

**ScoringSystem.ScoreWord returns 0:** This is a valid return value (e.g. a word composed entirely of zero-value letters in a custom scoring table). The flow proceeds normally. `OnWordSubmitted` fires with score = 0.

**Player rapidly clicks reels during Submitting state:** All reel-click inputs are ignored while in Submitting state. The submission pipeline runs to completion before input is accepted again.

**Degenerate strategy — intentional invalid submissions:** A player may deliberately submit invalid words to consume turns without advancing reels (e.g. to stall). This is not a degenerate exploit because the turn cost is symmetric — an invalid submission is strictly worse than a valid one (no score, no board progress). The Turn Manager's decrement on every attempt already penalizes this behavior through opportunity cost.

---

## Dependencies

### Provided to Other Systems (Outbound Events)

| Event | Signature | Consumer(s) |
|-------|-----------|-------------|
| `OnSelectionChanged` | `(List<int> selectedIndices, string currentWord)` | Board UI, Word Input UI |
| `OnSubmissionAttempted` | `(string word, bool isValid)` | Turn Manager, Timer System, Board UI, Word Input UI |
| `OnWordSubmitted` | `(string word, int score, List<int> advancedReelIndices)` | Board UI, Score UI, Timer System, Audio Manager |

### Required from Other Systems (Inbound Calls)

| System | What Board Manager Requires |
|--------|-----------------------------|
| ReelController (x6) | `CurrentChar` property (char), `Advance()` method |
| WordValidator | `Validate(string word): bool` method |
| ScoringSystem | `ScoreWord(string word): int` method |

### Lifecycle Dependency

Board Manager must be initialized after all 6 ReelControllers are initialized and after WordValidator and ScoringSystem are ready. Board Manager must be initialized before Turn Manager, Timer System, Game Mode Manager, Board UI, and Word Input UI subscribe to its events.

---

## Tuning Knobs

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `InitialReelCount` | Gate | 6 | 4–8 | Number of reels on the board. Affects word length possibility space and complexity. |
| `MinSelectionSize` | Gate | 1 | 1–3 | Minimum reels required for a submission. Increasing to 2 or 3 forces longer words and raises average score per turn. |
| `MaxSelectionSize` | Gate | 6 | 2–6 | Maximum reels selectable. Reducing creates an artificial word-length cap, constraining late-game optimization. |
| `AllowSingleCharWords` | Gate | true | true/false | Whether 1-reel submissions are attempted. Setting false enforces `MinSelectionSize >= 2` effectively. |

All tuning knobs are defined in `assets/data/board-config.json`. No values are hardcoded in the Board Manager implementation.

---

## Acceptance Criteria

### Functional Criteria

- **FC-BM-01**: Selecting reel 2 then reel 3 results in `_selectionStart = 2`, `_selectionEnd = 3`, word string = concat of reel 2 and 3 CurrentChars.
- **FC-BM-02**: Attempting to select reel 0 and reel 2 (skipping reel 1) is rejected; selection remains unchanged.
- **FC-BM-03**: Submitting an invalid word fires `OnSubmissionAttempted(word, false)` and does NOT fire `OnWordSubmitted`. No reels advance.
- **FC-BM-04**: Submitting a valid word fires `OnSubmissionAttempted(word, true)` then `OnWordSubmitted(word, score, advancedIndices)`. Only the selected reels' `Advance()` is called, exactly once each.
- **FC-BM-05**: After any submission (valid or invalid), `SelectedReelIndices` is empty and state is `Idle`.
- **FC-BM-06**: Submit called while in `Idle` state produces no events and no state change.
- **FC-BM-07**: Submit called while in `Submitting` state produces no events and no state change.
- **FC-BM-08**: `OnSelectionChanged` fires every time the selection set changes (reel added or removed), with the correct current word string.
- **FC-BM-09**: A 6-reel valid word advances all 6 ReelControllers exactly once.
- **FC-BM-10**: Selecting and deselecting reels does not advance any ReelController.

### Experiential Criteria (Playtest Validation)

- **EC-BM-01**: Players understand within 2 minutes of first play which reels are currently selected, without a tutorial. (Validated by observing first-session players without instruction.)
- **EC-BM-02**: Players do not experience any visible delay between pressing Submit and the board updating. (Target: board state update visible within 1 frame of input.)
- **EC-BM-03**: After a valid submission, players can immediately identify which reels advanced and which did not. (Validated by post-session interview: "could you tell which letters changed?")

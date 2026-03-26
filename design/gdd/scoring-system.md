# Scoring System

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Satisfying Progression — every word found visibly changes the board

---

## Overview

The Scoring System calculates the point value of a valid word by summing the Scrabble-style letter values for each character in the word, using the Letter Value Table data asset. It maintains a running `SessionScore` that accumulates across all valid words in a single game session. On each scoring event, it fires an `OnScoreChanged` event that carries both the word score and the updated session total, allowing any number of UI subscribers to update without polling. The system does not reset between words — only a full game restart resets the session score. For MVP, scoring is purely additive with no multipliers or bonuses; a length-bonus multiplier is specified as a post-MVP tuning knob.

---

## Player Fantasy

The player should feel that clever, rare words are meaningfully rewarded over common short ones. When a player constructs "QUARTZ" or "JAZZ" and sees a large number pop up, they should feel like a word expert who recognized an opportunity others would miss. The score is not just a number — it is the tangible measure of vocabulary depth and board-reading skill.

Each word score pop-up should feel like a mini-celebration: the score is immediate positive reinforcement for a good play. The session total climbing steadily should feel like momentum building. The player should be motivated to seek high-value letters (Q, Z, X, J) even when lower-value words are available, creating interesting tension between "safe" and "risky" plays.

Primary MDA aesthetics served: **Challenge** (optimizing letter value within board constraints) and **Expression** (vocabulary knowledge as a form of personal expression and skill demonstration).

---

## Detailed Design

### Core Rules

1. The Scoring System is called by the Board Manager only when word validation returns `IsValid: true` and the word is not a duplicate (both conditions are the Board Manager's gate; the Scoring System trusts that it is only called for fully approved words).
2. The system calculates a `WordScore` by summing `LetterValue(c)` for every character `c` in the validated word string.
3. The `LetterValue(c)` lookup reads from the Letter Value Table data asset. Every letter A–Z has a defined value. No letter returns a null or missing value.
4. `SessionScore` is initialized to 0 at game start and incremented by `WordScore` on each scoring call. `SessionScore` never decreases during a session.
5. `WordsFoundCount` is initialized to 0 at game start and incremented by 1 on each call to `ScoreWord()`. It tracks the number of valid words found this session.
6. After updating `SessionScore` and `WordsFoundCount`, the system fires `OnScoreChanged(int sessionScore, int wordScore)`. This event fires synchronously, before control returns to the Board Manager. No screen coordinates are included — flyout spawn position is the Score UI's responsibility (see Score UI GDD).
7. The system exposes `SessionScore` and `WordsFoundCount` as read-only properties for the Game Over Screen to read without subscribing to events.
8. The Scoring System has no concept of turns, time, or game mode. It scores words; the Board Manager decides when to call it.
9. A `Reset()` method exists to set `SessionScore` and `WordsFoundCount` back to 0 and fire `OnScoreChanged(0, 0)`. This is called by the Board Manager at game start (or restart). It is not called between words.

### Letter Value Table (Standard Scrabble Values)

| Value | Letters |
|-------|---------|
| 1 | A, E, I, O, U, L, N, S, T, R |
| 2 | D, G |
| 3 | B, C, M, P |
| 4 | F, H, V, W, Y |
| 5 | K |
| 8 | J, X |
| 10 | Q, Z |

These values are defined in the Letter Value Table data asset (`assets/data/letter-values.json`). They are the standard Scrabble distribution and are presented here for reference. The Scoring System reads from the asset — if the asset values differ from this table, the asset is authoritative.

### States and Transitions

The Scoring System has two logical states:

```
ScoringState:
  Inactive    — game not in session; SessionScore = 0; no events fired
  Active      — game in session; accepts scoring calls; fires events
```

**Transition rules:**

| From | To | Trigger |
|------|----|---------|
| Inactive | Active | `Reset()` called by Board Manager at game start |
| Active | Inactive | Game session ends (Game State Machine transitions to GameOver state) |
| Active | Active | `ScoreWord(word)` called; SessionScore updates; OnScoreChanged fires |

In practice, the Scoring System does not enforce this state machine at runtime (calling `ScoreWord` while `Inactive` would simply add to the score, which cannot occur in normal gameplay since the Board Manager only calls it during active play). The state distinction is documented for clarity of intent and for unit test setup.

### Interactions with Other Systems

- **Letter Value Table (read-only):** The Scoring System reads letter values from this data asset at scoring time (or optionally caches the full lookup table at initialization — see Tuning Knobs). It does not write to the asset.
- **Board Manager (caller):** The Board Manager calls `ScoreWord(string word)` after confirming a word is valid and not a duplicate. The Board Manager also calls `Reset()` at game start. The Scoring System does not hold a reference to the Board Manager.
- **Score UI (subscriber):** Subscribes to `OnScoreChanged` to update the displayed session score and show the word-score pop-up animation. The Score UI reads the event parameters — it does not call `SessionScore` directly except at game start to initialize the display.
- **Game Over Screen (reader):** Reads `SessionScore` directly at game end to display the final score. It may also subscribe to `OnScoreChanged` if it wants to display a live-updating score during a "game over" transition animation.

---

## Formulas

### Word Score Formula

```
WordScore = Σ LetterValue(c) for c in word
```

**Variables:**
- `word`: `string` — the validated word, uppercase, length 2–6
- `c`: `char` — each individual character in `word`, range 'A'–'Z'
- `LetterValue(c)`: `int` — point value for character `c`, looked up from Letter Value Table. Range: 1–10.
- `WordScore`: `int` — sum of all letter values in the word. Minimum: 2 (two 1-point letters). Maximum: 60 (six Z's, value 10 each — impossible in practice since the Trie would reject it, but mathematically bounded).

**Example — "PUZZLE":**
```
P = 3
U = 1
Z = 10
Z = 10
L = 1
E = 1
WordScore = 3 + 1 + 10 + 10 + 1 + 1 = 26
```

**Example — "REEL":**
```
R = 1
E = 1
E = 1
L = 1
WordScore = 1 + 1 + 1 + 1 = 4
```

**Example — "QUARTZ":**
```
Q = 10
U = 1
A = 1
R = 1
T = 1
Z = 10
WordScore = 10 + 1 + 1 + 1 + 1 + 10 = 24
```

### Session Score Formula

```
SessionScore(n) = SessionScore(n-1) + WordScore(n)
SessionScore(0) = 0
```

**Variables:**
- `n`: word submission number within the session (1-indexed)
- `SessionScore(n)`: running total after the nth valid word

### Practical Score Range

Given standard turn limits and realistic word-finding behavior:

| Scenario | Turns | Avg WordScore | SessionScore |
|---------|-------|---------------|--------------|
| Beginner | 20 | 5 | ~100 |
| Average | 20 | 8 | ~160 |
| Expert | 20 | 12 | ~240 |
| Perfect (theoretical) | 20 | 20 | ~400 |

These ranges are design targets, not enforced limits. They inform UI scaling decisions (score display should comfortably render 4-digit numbers).

### Post-MVP: Length Bonus Multiplier (Tuning Knob, disabled for MVP)

```
WordScore = (Σ LetterValue(c) for c in word) × LengthMultiplier(word.Length)
```

Where `LengthMultiplier` is a lookup table:

| Word Length | Multiplier (example values) |
|------------|----------------------------|
| 2 | 1.0× |
| 3 | 1.0× |
| 4 | 1.25× |
| 5 | 1.5× |
| 6 | 2.0× |

For MVP, `LengthMultiplier` is always 1.0 for all lengths. The multiplier table is present in the config but all values set to 1.0.

---

## Edge Cases

**Zero-value word:** Not possible with the standard letter value table (minimum 1 point per letter) and minimum word length of 2. Minimum possible `WordScore` is 2. No guard needed.

**Maximum word score overflow:** The maximum theoretical `WordScore` per word is 60 (six Z's). The maximum theoretical `SessionScore` with 100 turns is 6,000. Both fit comfortably in a 32-bit `int`. No overflow protection needed.

**`ScoreWord()` called with an empty string:** This should not occur (Board Manager gate ensures only valid, non-empty words reach scoring). If called with an empty string, the sum loop produces `WordScore = 0`. `SessionScore` increments by 0. `OnScoreChanged(sessionScore, 0)` fires. No exception. The behavior is defined and harmless, though the design intent is that this call should never happen.

**`ScoreWord()` called with a character not in the Letter Value Table (e.g., a digit or punctuation):** `LetterValue(c)` would return a missing-key result. The system must handle this defensively: `LetterValue(c)` should return 0 for any character outside A–Z, with a debug-build warning logged. In practice this cannot occur through normal gameplay since Reel Sequences only contain A–Z characters.

**`OnScoreChanged` has no subscribers:** The event fires and no handlers are invoked. This is valid behavior. `SessionScore` still updates correctly. This scenario occurs in unit tests and during early engine startup before UI subscribes.

**`Reset()` called mid-session:** `SessionScore` returns to 0. `OnScoreChanged(0, 0)` fires. Any subscribed UI will reset to zero. This is intentional behavior for the "restart game" flow. The design does not support "undo" or partial resets.

**Calling `ScoreWord()` before `Reset()` is ever called (e.g., at engine startup):** `SessionScore` starts at 0 by default (C# field initialization). Calling `ScoreWord()` before `Reset()` would add to a score of 0, which is functionally the same as a fresh session. No corruption occurs. The Board Manager should call `Reset()` at game start as a matter of hygiene, but the Scoring System is resilient to this order.

**Duplicate word scored twice (if Board Manager's duplicate gate fails):** The Scoring System has no duplicate detection. If `ScoreWord("REEL")` is called twice, `SessionScore` increases by `WordScore("REEL")` twice. This is a bug in the Board Manager, not a Scoring System concern. The Scoring System documents this limitation so it is testable: a test that calls `ScoreWord("REEL")` twice and checks `SessionScore == 2 × WordScore("REEL")` would confirm the system has no implicit deduplication.

---

## Dependencies

### What This System Requires

| Dependency | Type | Contract |
|-----------|------|---------|
| Letter Value Table | Data Asset | Provides `int GetValue(char letter)` lookup for all A–Z characters. Must be loaded before the first call to `ScoreWord()`. Injected at construction time or loaded in `Awake()`. |

### What This System Provides

| Consumer | Provided Interface |
|---------|-------------------|
| Board Manager | `void ScoreWord(string word)` — scores a word and updates SessionScore and WordsFoundCount |
| Board Manager | `void Reset()` — resets SessionScore and WordsFoundCount to 0, fires OnScoreChanged |
| Score UI | `event OnScoreChanged(int sessionScore, int wordScore)` — fires after every score update (no screen coords — Score UI resolves position via Board UI) |
| Score UI | `int SessionScore { get; }` — current session total (read-only) |
| Game Over Screen | `int SessionScore { get; }` — final score for display |
| Game Over Screen | `int WordsFoundCount { get; }` — number of valid words found this session |

### Bidirectional Dependency Notes

- **Letter Value Table** is depended on by Scoring System. The Letter Value Table GDD must list Scoring System as a consumer.
- **Board Manager** calls Scoring System. The Board Manager GDD must list Scoring System as a dependency and document that it is the sole arbiter of when `ScoreWord()` is called (only for valid, non-duplicate words).
- **Score UI** subscribes to Scoring System events. The Score UI GDD must list Scoring System as a dependency.
- **Game Over Screen** reads `SessionScore` from Scoring System. The Game Over Screen GDD must list Scoring System as a dependency.

---

## Tuning Knobs

All tuning values live in `assets/data/scoring-config.json`. No values are hardcoded.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|-----------|--------|
| `letterValues` | Curve | Standard Scrabble (see table above) | 1–10 per letter | Controls relative word value distribution. Increasing high-value letter scores (Q, Z, X, J) widens the score gap between expert and average players. Decreasing them flattens the distribution. Full redefinition possible for a custom letter economy. |
| `lengthMultiplierTable` | Curve | All 1.0 (MVP) | 1.0–3.0 per length | Post-MVP bonus multiplier by word length. Incentivizes longer words. Values above 2.0 for 6-letter words may over-reward length at the expense of letter value strategy. |
| *(flyout duration)* | — | — | — | Flyout animation duration is a Score UI concern, tuned in `ui-config.json` under `score_ui`. Not owned by Scoring System. |
| `enableLengthBonus` | Gate | false (MVP) | true / false | Master toggle for the length bonus multiplier. When false, `LengthMultiplier` is always 1.0 regardless of the table values. Allows the bonus to be authored and tested without activating it. |

---

## Acceptance Criteria

### Functional Criteria

- [ ] `ScoreWord("PUZZLE")` returns `WordScore = 26` and increments `SessionScore` by 26
- [ ] `ScoreWord("REEL")` returns `WordScore = 4` (R=1, E=1, E=1, L=1)
- [ ] `ScoreWord("QUARTZ")` returns `WordScore = 24` (Q=10, U=1, A=1, R=1, T=1, Z=10)
- [ ] After `Reset()`, `SessionScore == 0`
- [ ] After `Reset()` followed by `ScoreWord("REEL")`, `SessionScore == 4`
- [ ] After two valid word scores of 4 and 26, `SessionScore == 30`
- [ ] `OnScoreChanged` fires exactly once per `ScoreWord()` call
- [ ] `OnScoreChanged` fires with `sessionScore = new total` and `wordScore = this word's score`
- [ ] `OnScoreChanged(0, 0)` fires when `Reset()` is called
- [ ] `ScoreWord()` with an empty string produces `WordScore = 0` and does not throw
- [ ] `SessionScore` property returns the same value as the last `sessionScore` parameter passed to `OnScoreChanged`
- [ ] Letter values are read from the data asset, not hardcoded — modifying `letter-values.json` and reloading changes scoring behavior without code changes

### Experiential Criteria (Playtesting)

- [ ] Players can predict the score of a word before submitting it (implies letter values are learnable and intuitive)
- [ ] High-value words (containing Q, Z, X, J) produce a noticeably larger score pop-up number than common words — the visual feedback matches the emotional reward
- [ ] Players report that the score feels "fair" — common words score less than uncommon words, as expected
- [ ] In a 10-player session, at least 3 players attempt to construct a word specifically because it contains a high-value letter (evidence that the incentive structure is working)

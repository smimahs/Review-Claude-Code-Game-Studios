# Letter Value Table

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic clarity — the point value of any letter is fixed, public knowledge, and consistent; the player can always calculate their expected score before submitting

---

## Overview

The Letter Value Table is a Unity ScriptableObject that maps each of the 26 uppercase Latin letters (A-Z) to an integer point value. The values are sourced from the North American Scrabble (TWL) standard, which is deeply familiar to word game players and has been validated over decades of play as a sensible proxy for letter rarity in English. The Scoring System reads from this asset to calculate word scores: the score for a word is the sum of the point values of its constituent letters. The asset is a single file, assigned once in the Inspector, and is read-only at runtime. It requires no logic — it is pure data that answers exactly one question: "how many points is this letter worth?"

---

## Player Fantasy

The player should feel like they are **hunting for value**. When scanning the board, a player who has internalized the letter values thinks "that Q is worth 10 points — if I can build a word around Reel 3 when it hits Q, that's a big score." The value table transforms the board from an abstract character grid into a **landscape of opportunity** where some positions are more desirable than others. This creates the satisfying pre-submission ritual of mentally tallying a word's score before committing. Players feel clever when they deliberately construct a high-value word and see their mental math confirmed by the score pop-up. The values must be consistent and learnable — if a player memorizes that K=5 in session one, that knowledge must still be correct in session ten.

---

## Detailed Design

### Core Rules

1. The `LetterValueTable` ScriptableObject contains exactly one `int[]` of length 26, indexed 0-25 where index 0 = A, index 1 = B, ..., index 25 = Z.
2. All 26 values must be positive integers (minimum 1). Zero and negative values are invalid and blocked by `OnValidate()` in the Unity Editor.
3. The values shipped with the game are the standard North American Scrabble (TWL) letter values. See the Formulas section for the complete table.
4. The Scoring System accesses a letter's value by computing `values[letter - 'A']` where `letter` is an uppercase char. This is an O(1) array lookup with no branching.
5. No letter has a null or missing value. The array is always exactly 26 entries. There is no default fallback — a missing value is a data error, not a runtime condition.
6. The ScriptableObject is assigned to the Scoring System via the Unity Inspector. The Scoring System holds the reference and is the sole consumer.
7. The asset is read-only at runtime. No system writes to the `LetterValueTable` after initialization.
8. The asset provides a `GetValue(char letter)` helper method that accepts any char, uppercases it, validates it is A-Z, and returns the corresponding integer value. This method is also exposed for use in UI preview systems (e.g., tooltips showing letter values on hover).
9. `GetValue()` called with a non-A-Z character returns 0 and logs a Unity warning: `"[LetterValueTable] GetValue called with non-alpha character '{c}'. Returning 0."` This should never occur in normal gameplay; the warning exists to surface integration errors.

### States and Transitions

This system is stateless. It is a pure lookup table with no mutable runtime state.

| State | Description |
|-------|-------------|
| Loaded | ScriptableObject instantiated by Unity; values array populated from serialized data; all calls to `GetValue()` are valid |

There is no unloaded state at runtime. If the asset is not assigned in the Inspector, the Scoring System detects a null reference and logs an error (see Edge Cases).

### Interactions with Other Systems

**Scoring System (downstream consumer)**
- The Scoring System holds a serialized reference to the `LetterValueTable` asset (assigned in Inspector).
- Data flow: Scoring System provides a char (one letter of the submitted word); `LetterValueTable.GetValue(char)` returns an int.
- The Scoring System calls `GetValue()` once per letter in the submitted word and sums the results to produce the word score.
- The Scoring System does not cache letter values — it queries the asset per word submission. This is acceptable because word submissions are infrequent events (not per-frame), and array lookups are O(1).
- The `LetterValueTable` does not call back into the Scoring System; data flows one way.

**Board UI / Word Preview (indirect future consumer)**
- Post-MVP: a running score preview might display the current word's projected value as the player selects reels. This UI would call `GetValue()` directly, or via a Scoring System method that internally uses `GetValue()`. The Letter Value Table's `GetValue()` is public precisely to enable this use case.

---

## Formulas

### Word Score Formula

```
WordScore = sum of GetValue(letter) for each letter in the submitted word
```

Where:
- The submitted word is the concatenated characters from the selected reels (already validated as A-Z uppercase by the Word Validator)
- `GetValue(letter)` returns the integer value from the table below
- Minimum word score: 2 (two 1-point letters, e.g., "AT")
- Maximum word score (6-letter, all high-value letters): 60 (six Q's = 60, but Q-Q-Q-Q-Q-Q is not a valid English word; practical maximum from real words is lower — see examples)

### Complete Letter Value Table (TWL / North American Scrabble Standard)

| Letter | Value | Letter | Value | Letter | Value |
|--------|-------|--------|-------|--------|-------|
| A | 1 | J | 8 | S | 1 |
| B | 3 | K | 5 | T | 1 |
| C | 3 | L | 1 | U | 1 |
| D | 2 | M | 3 | V | 4 |
| E | 1 | N | 1 | W | 4 |
| F | 4 | O | 1 | X | 8 |
| G | 2 | P | 3 | Y | 4 |
| H | 4 | Q | 10 | Z | 10 |
| I | 1 | R | 1 | — | — |

**Value distribution summary:**

| Point Value | Letters | Count |
|-------------|---------|-------|
| 1 | A, E, I, L, N, O, R, S, T, U | 10 |
| 2 | D, G | 2 |
| 3 | B, C, M, P | 4 |
| 4 | F, H, V, W, Y | 5 |
| 5 | K | 1 |
| 8 | J, X | 2 |
| 10 | Q, Z | 2 |

Total letters: 26. Note that the 10 most common English letters are all worth 1 point, reflecting that Scrabble values encode rarity, not frequency — the rarer the letter in English, the higher its value.

### Example Word Score Calculations

| Word | Letters | Calculation | Total |
|------|---------|-------------|-------|
| CAT | C(3) + A(1) + T(1) | 3 + 1 + 1 | 5 |
| WORD | W(4) + O(1) + R(1) + D(2) | 4 + 1 + 1 + 2 | 8 |
| STONE | S(1) + T(1) + O(1) + N(1) + E(1) | 1+1+1+1+1 | 5 |
| BLANK | B(3) + L(1) + A(1) + N(1) + K(5) | 3+1+1+1+5 | 11 |
| JIFFY | J(8) + I(1) + F(4) + F(4) + Y(4) | 8+1+4+4+4 | 21 |
| QUIRKY | Q(10)+U(1)+I(1)+R(1)+K(5)+Y(4) | 10+1+1+1+5+4 | 22 |

The example "QUIRKY" at 22 points illustrates the score ceiling for real 6-letter words. Words using common letters (e.g., "STONE" = 5) and words using rare letters (e.g., "QUIRKY" = 22) represent the practical scoring range. Most submitted words will fall in the 5-15 range given the v1.0 reel sequences (which do not include Q or Z).

### Internal Array Layout (for programmer reference)

```
index: 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25
letter: A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z
value:  1  3  3  2  1  4  2  4  1  8  5  1  3  1  1  3 10  1  1  1  1  4  4  8  4 10
```

Access pattern: `values['Q' - 'A'] == values[16] == 10`

---

## Edge Cases

**Non-A-Z character passed to GetValue**
Returns 0 and logs a Unity warning. This should not occur in gameplay because the Word Validator guarantees only A-Z characters reach the Scoring System. The 0-return is a safe fallback that does not crash and does not inflate the score. The warning flags the integration error for developers.

**ScriptableObject not assigned in Inspector (null reference)**
If the Scoring System's `LetterValueTable` reference is null, any call to `GetValue()` will throw a NullReferenceException in the Scoring System (not in this asset). The Scoring System must null-check its reference during initialization and log an error: `"[ScoringSystem] LetterValueTable asset not assigned. Scoring will not function."` This is a developer error (missing Inspector assignment), not a player-facing condition.

**Values array has fewer than 26 entries (corrupted asset)**
If the serialized array has fewer than 26 entries (e.g., due to asset file corruption), an access to `values[index]` for a high-index letter (e.g., Z = index 25) will throw an `IndexOutOfRangeException`. The `OnValidate()` method enforces exactly 26 entries in the Editor. If the asset is corrupt at runtime, the exception will surface in QA before shipping.

**All values set to 1 (flat table variant)**
Technically valid per the rules (all values >= 1). Produces a scoring system where word score equals word length. This eliminates the strategic layer of hunting for high-value letters. This is an intentional tuning option (see Tuning Knobs) but not the default.

**Zero or negative value entered in Editor**
Blocked by `OnValidate()` with a Unity Editor warning and the value reset to 1. Negative values would allow submitted words to reduce the score, which contradicts the game's scoring model.

**Word of length 1 submitted**
The Board Manager enforces a minimum selection of 2 reels, so single-character words cannot reach the Scoring System under normal game flow. If they somehow do (a bug elsewhere), `GetValue()` is called once and returns a valid integer. No error occurs in this system.

**Word of length 6 submitted (maximum)**
All 6 `GetValue()` calls complete normally. No special handling required. The maximum theoretical single-word score from the v1.0 sequences (no Q or Z) would be a 6-letter word using J and X alongside common consonants, e.g., a word containing J, X, F, H, W, Y = 8+8+4+4+4+4 = 32 points. In practice, no common English word uses this exact combination; the realistic 6-letter maximum from common words is approximately 20-25 points.

---

## Dependencies

**Upstream (what this system depends on)**
- None. The ScriptableObject is pure authored data with no runtime code dependencies.

**Downstream (what depends on this system)**
- **Scoring System** — the sole consumer at MVP. Holds the asset reference (assigned in Inspector). Calls `GetValue(char)` once per letter of every submitted word. The Scoring System owns all score accumulation logic; the Letter Value Table only provides per-letter values.
- **Board UI / Word Preview UI (Post-MVP)** — may call `GetValue()` directly to display per-letter point values on the board (e.g., showing "K=5" when the player hovers over a reel displaying K). This is a read-only, display-only usage.

**No circular dependencies.** This asset has no knowledge of any system that uses it.

---

## Tuning Knobs

| Knob | Category | Current Value | Safe Range | What Changes at Extremes |
|------|----------|---------------|------------|--------------------------|
| Individual letter values (all 26) | Content | TWL Scrabble standard (see table) | 1-20 per letter | Lowering high-value letters (J, Q, X, Z) reduces score variance and reduces incentive to plan for rare-letter moments; raising common letters (E, A, T) inflates scores uniformly without adding strategic depth |
| Value spread (difference between lowest and highest value) | Content | 1 to 10 (spread of 9) | 1 to 5 (flat), 1 to 20 (steep) | Flat spread: all words score similarly, less strategic differentiation; steep spread: score is dominated by a single rare letter hit, reducing the value of multi-letter planning |
| Scoring model variant | Gate | Per-letter sum (standard) | Per-letter sum, per-letter sum x word-length multiplier, per-letter sum x word-length bonus | Multipliers reward longer words more aggressively; considered a Post-MVP option if playtesting shows players preferring short high-value words over longer words |

**Note on custom values:** The TWL Scrabble values are the recommended default because they are a known-good, widely validated distribution. Custom values are a meaningful tuning lever only after base game feel is validated through playtesting. Changing letter values mid-playtest invalidates all prior score comparisons.

---

## Acceptance Criteria

### Functional Criteria (automated NUnit tests)

| # | Test | Pass Condition |
|---|------|---------------|
| AC-1 | Asset has exactly 26 values | `letterValueTable.values.Length == 26` |
| AC-2 | All values are >= 1 | For every entry `v` in the array, `v >= 1` |
| AC-3 | Specific value spot-checks (TWL standard) | `GetValue('A') == 1`, `GetValue('E') == 1`, `GetValue('J') == 8`, `GetValue('K') == 5`, `GetValue('Q') == 10`, `GetValue('Z') == 10` |
| AC-4 | Full alphabet coverage | `GetValue()` returns a value >= 1 for every character A through Z with no exceptions |
| AC-5 | Non-alpha returns 0, no exception | `GetValue('1')` returns 0 and does not throw; `GetValue(' ')` returns 0 and does not throw |
| AC-6 | Lowercase input handled | `GetValue('a')` returns the same value as `GetValue('A')` (case normalization inside GetValue) |
| AC-7 | Word score calculation: known word | Score of "CAT" == 5; score of "QUIRKY" == 22 |
| AC-8 | Word score calculation: minimum | Score of a 2-letter all-1-value word (e.g., "AT") == 2 |
| AC-9 | Index formula is correct | `values['Q' - 'A'] == values[16] == 10` |

### Experiential Criteria (verified by playtesting)

| # | Criterion | How to Verify |
|---|-----------|---------------|
| AC-10 | High-value letters feel rewarding | When a player submits a word containing J, K, X, or Z, they verbally acknowledge the high score or express satisfaction; observed in at least 3 out of 5 playtesters |
| AC-11 | Common-letter words still feel worthwhile | Playtesters continue submitting common-letter words (scoring 4-8 points) without expressing frustration; they do not exclusively hold out for rare-letter words |
| AC-12 | Values are learnable | After 15 minutes of play, at least 3 out of 5 playtesters can correctly identify which of two displayed letters (e.g., K vs. E) is worth more points, without consulting a reference |
| AC-13 | Score pop-up is readable | When a word is submitted, the word score appears clearly in the UI and the player can verify it mentally against the letters used; no playtester reports a score that "seems wrong" for a standard word |

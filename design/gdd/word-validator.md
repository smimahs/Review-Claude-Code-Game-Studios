# Word Validator

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic Clarity — board state always legible; player knows exactly what will happen

---

## Overview

The Word Validator is a stateless service that takes an ordered list of Reel Controller references, reads the current character from each, concatenates them into a candidate word string, and queries the Trie Dictionary to determine whether the word is valid. It returns a `ValidationResult` value object containing a boolean validity flag, the candidate word string, and a human-readable reason code. The validator enforces minimum word length before querying the Trie. It has no side effects: it does not advance reels, does not update scores, does not track history, and does not modify any external state. The Board Manager is the sole caller and is responsible for acting on the result.

**Design decision — duplicate detection boundary:** Duplicate word detection (rejecting a word already submitted this session) is the Board Manager's responsibility, not the Word Validator's. The validator is intentionally kept stateless and pure so it can be instantiated once, tested without session context, and reused across game modes. The Board Manager maintains the submitted-words hash set and checks it before or after calling the validator. This boundary is documented in the Dependencies section.

---

## Player Fantasy

The player should feel that the validation system is an honest referee: it will tell them immediately and clearly whether their word counts, and it will always give a reason if it does not. There should be no mystery about why a submission failed. "Not in dictionary" is a clear answer. "Too short" is a clear answer. The player should never feel cheated by an opaque rejection.

The validator's speed (O(k) Trie lookup where k is word length) ensures the feedback is instantaneous — validation should never feel like it is "thinking." The result must be communicated to the player within one frame of the submission action.

Primary MDA aesthetics served: **Challenge** (the player tests their word knowledge against a defined lexicon) and **Fantasy** (the player acts as a word expert whose judgments are immediately confirmed or denied by an authoritative source).

---

## Detailed Design

### Core Rules

1. The validator accepts an ordered list of Reel Controller references representing the player's selected contiguous subset of reels, ordered left to right (lowest reel index to highest).
2. The validator reads `CurrentChar` from each Reel Controller in the list and concatenates the characters in order to form the candidate word string.
3. The candidate word string is converted to uppercase before any comparison or lookup. All Trie entries are stored uppercase. Case is never a validation failure reason.
4. Minimum length check: if the candidate word has fewer than `MinWordLength` characters (default: 2), validation fails immediately with reason `"TooShort"`. The Trie is not queried.
5. Maximum length check: if the candidate word has more than 6 characters (the board has 6 reels), validation fails with reason `"TooLong"`. In practice this cannot occur through normal gameplay (the player can select at most 6 reels), but the check guards against programmatic misuse.
6. Trie lookup: `TrieDictionary.Contains(candidateWord)` is called. If it returns `false`, validation fails with reason `"NotInDictionary"`.
7. If all checks pass, validation succeeds with reason `"Valid"`.
8. The validator returns a `ValidationResult` struct. It does not throw exceptions for invalid words — rejection is an expected, normal outcome.
9. The validator is stateless. It holds no session data, no history, and no reference to game state beyond the injected Trie Dictionary instance. The same validator instance can be called multiple times per turn or reused across multiple game sessions without reset.

### The ValidationResult Contract

```
struct ValidationResult
{
    bool   IsValid      // true only if all checks passed
    string Word         // the uppercase candidate word (always populated, even on failure)
    string ReasonCode   // machine-readable code for downstream logic
    string ReasonText   // human-readable text for UI display
}
```

**Reason codes and their meanings:**

| `ReasonCode` | `ReasonText` (display example) | Condition |
|-------------|-------------------------------|-----------|
| `"Valid"` | "Valid!" | All checks passed |
| `"TooShort"` | "Need at least 2 letters" | Word length < `MinWordLength` |
| `"TooLong"` | "Too many letters" | Word length > 6 (guard only) |
| `"NotInDictionary"` | "Not a word" | Trie lookup returned false |
| `"EmptySelection"` | "No reels selected" | Input list is null or empty |

The Board Manager uses `ReasonCode` for logic branching (e.g., deciding whether to consume a turn). The Score UI and Word Input UI use `ReasonText` for player-facing messages. The Board Manager may augment `ReasonText` before display (e.g., appending the duplicate-word message), but the validator never produces duplicate-detection reason codes — that is strictly the Board Manager's domain.

### Validation Sequence (ordered checks)

```
1. Null / empty input guard      → ReasonCode: "EmptySelection"
2. Build candidate word string   → read CurrentChar from each reel, concatenate, uppercase
3. Minimum length check          → ReasonCode: "TooShort"
4. Maximum length check          → ReasonCode: "TooLong"  (guard)
5. Trie dictionary lookup        → ReasonCode: "NotInDictionary"
6. All passed                    → ReasonCode: "Valid", IsValid: true
```

Checks are evaluated in order and short-circuit on first failure. This ordering is intentional: cheap structural checks run before the Trie lookup (which, while O(k) and fast, still traverses the Trie). The ordering also produces the most useful error message — "too short" is a more actionable message than "not in dictionary" for a one-letter selection.

### States and Transitions

The Word Validator has no internal state machine. It is a pure function: given the same list of Reel Controllers in the same state, it always returns the same result. There is no "validating" transitional state — the operation is synchronous and completes within a single frame.

If an asynchronous dictionary (e.g., server-side validation) were added in a future version, this section would need revision. For MVP, synchronous Trie lookup is the only implementation.

### Interactions with Other Systems

- **Reel Controllers (read-only):** The validator reads `CurrentChar` from each provided controller. It calls no other methods and changes no state on the controllers.
- **Trie Dictionary (read-only):** The validator calls `TrieDictionary.Contains(word)`. It does not insert, delete, or iterate. The Trie Dictionary instance is injected at construction time (constructor injection, not singleton access) to keep the validator unit-testable with a mock or stub dictionary.
- **Board Manager (caller):** The Board Manager constructs the ordered list of selected Reel Controllers and calls `Validate(reelControllers)`. The Board Manager owns the turn-consumption decision and the duplicate-detection gate. The validator does not hold a reference to the Board Manager.

---

## Formulas

### Candidate Word Construction

```
candidateWord = ""
for each ReelController rc in selectedReels (ordered left-to-right):
    candidateWord += rc.CurrentChar
candidateWord = candidateWord.ToUpper()
```

**Variables:**
- `selectedReels`: `List<ReelController>` — ordered subset of the 6 reels, length 1–6
- `rc.CurrentChar`: `char` — the character currently shown on reel `rc`
- `candidateWord`: `string` — the resulting candidate word, length equals `selectedReels.Count`

**Example:**
Selected reels in order show characters: R, E, E, L
`candidateWord = "REEL"` — length 4, passes minimum length check, submitted to Trie.

### Validation Time Complexity

```
T(k) = O(k)
```

Where `k` = word length (1–6). The Trie lookup traverses at most 6 nodes. String construction is also O(k). Total validation time is O(k), bounded by O(6) = O(1) for this game.

### Minimum Length Check

```
if candidateWord.Length < MinWordLength → fail with "TooShort"
```

**Variables:**
- `MinWordLength`: `int` — configurable, default 2, range 1–4 (see Tuning Knobs)

**Rationale for default of 2:** Single-letter words are overwhelmingly likely to be accidental selections (player tapped one reel). All single-letter strings are also either not in the dictionary or trivially short (the word "a" and "I" are technically valid English words but submitting them would be degenerate low-effort play with minimal score impact). A minimum of 2 is a pragmatic guard. If playtesting reveals 2-letter words are also too easy to exploit (e.g., "AA", "AB"), the tuning knob allows raising to 3.

---

## Edge Cases

**Null or empty selected reels list:** `Validate(null)` and `Validate(new List<>())` both return `IsValid: false, ReasonCode: "EmptySelection"`. This should not occur in normal gameplay (the Submit button is disabled when no reels are selected — see Word Input UI), but the validator must be defensive because it is a shared service.

**Single reel selected:** If `MinWordLength` is 2 (default), a single reel produces a one-character word and fails with `"TooShort"`. If `MinWordLength` is 1, a single reel produces a one-character string that is then checked against the Trie (which will contain very few one-character entries). This edge case is fully covered by the minimum length check and requires no special handling.

**All 6 reels selected:** Valid input. The word is 6 characters long. Trie lookup proceeds normally. This is the maximum-length word possible in ReelWords.

**Duplicate characters in the word (e.g., "EERIE"):** The validator treats characters as a plain string. It does not care about repeated characters. "EERIE" is a valid dictionary word and would return `IsValid: true` if the Trie contains it.

**Candidate word that was already submitted this session:** The validator will return `IsValid: true` for a valid dictionary word regardless of submission history. Duplicate detection is the Board Manager's responsibility. The validator does not see submission history. See Dependencies for the boundary contract.

**Trie Dictionary not loaded (null reference):** If `TrieDictionary` is null at validation time, a `NullReferenceException` will propagate to the Board Manager. This is a programmer error (improper initialization), not a gameplay scenario. The Board Manager must assert that the Trie Dictionary is loaded before the game enters the Playing state.

**Non-alphabetic characters in a reel sequence:** The Reel Sequence Data spec must guarantee all sequence characters are A–Z. If a non-alphabetic character appears, the Trie lookup will return false (`"NotInDictionary"`). The validator does not validate the character set of input — it trusts the data layer.

**Case sensitivity:** `CurrentChar` values from Reel Controllers are always uppercase (as defined by Reel Sequence Data). The `.ToUpper()` call in the word construction step is a defensive belt-and-suspenders measure. It has no performance impact for a 6-character string.

---

## Dependencies

### What This System Requires

| Dependency | Type | Contract |
|-----------|------|---------|
| Trie Dictionary | Service (injected) | Exposes `bool Contains(string word)` method. Must be fully loaded before the first call to `Validate()`. Injected via constructor. |
| Reel Controller | Runtime Read | Exposes `char CurrentChar { get; }`. The validator reads this property; it does not call `Advance()` or `SetState()`. |

### What This System Provides

| Consumer | Provided Interface |
|---------|-------------------|
| Board Manager | `ValidationResult Validate(List<ReelController> selectedReels)` — returns a result struct with `IsValid`, `Word`, `ReasonCode`, `ReasonText` |

### Bidirectional Dependency Notes

- **Trie Dictionary** is depended on by Word Validator. The Trie Dictionary GDD must list Word Validator as a consumer.
- **Reel Controller** is read by Word Validator. The Reel Controller GDD lists Word Validator as a consumer of `CurrentChar`.
- **Board Manager** calls Word Validator. The Board Manager GDD must list Word Validator as a dependency and document that it owns duplicate-detection logic (not the validator).

### Duplicate Detection Boundary (Board Manager's Responsibility)

The Board Manager maintains a `HashSet<string> _submittedWords` (session-scoped). After calling `Validate()` and receiving `IsValid: true`, the Board Manager checks whether `_submittedWords.Contains(result.Word)`. If the word is a duplicate, the Board Manager constructs its own rejection response (reason: `"AlreadyPlayed"`) and does not call `Advance()` or the Scoring System. This check happens after validation because it makes semantic sense to confirm the word is valid before checking if it has been played — and it keeps the validator's concern strictly lexical.

---

## Tuning Knobs

All tuning values live in `assets/data/validator-config.json`. No values are hardcoded.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|-----------|--------|
| `minWordLength` | Gate | 2 | 1 – 4 | Minimum number of characters for a valid submission. Raising to 3 eliminates trivial 2-letter words (e.g., "AA", "OF", "TO") and increases average word quality and score. Lowering to 1 opens degenerate single-letter submissions. Value of 2 is recommended for MVP. |
| `maxWordLength` | Gate | 6 | 6 – 6 | Maximum word length, bounded by reel count. Not a practical tuning target unless reel count changes. Kept as a config value for future-proofing. |
| `caseSensitive` | Gate | false | — | Whether dictionary lookup is case-sensitive. Always false for ReelWords. Reserved as a config flag for unit-test flexibility. |

---

## Acceptance Criteria

### Functional Criteria

- [ ] `Validate()` with a null input list returns `IsValid: false` and `ReasonCode: "EmptySelection"`
- [ ] `Validate()` with an empty list returns `IsValid: false` and `ReasonCode: "EmptySelection"`
- [ ] A single reel selected (with `MinWordLength = 2`) returns `IsValid: false` and `ReasonCode: "TooShort"`
- [ ] A valid dictionary word of length 2 returns `IsValid: true` and `ReasonCode: "Valid"`
- [ ] A 4-letter string not in the dictionary returns `IsValid: false` and `ReasonCode: "NotInDictionary"`
- [ ] The `Word` field in the result is always uppercase regardless of what `CurrentChar` returns
- [ ] The `Word` field is populated even when `IsValid` is false
- [ ] Calling `Validate()` does not change the `_currentIndex` of any Reel Controller
- [ ] Calling `Validate()` twice with the same reel state returns the same result (idempotent)
- [ ] A 6-reel selection forming a valid word returns `IsValid: true`
- [ ] A valid word submitted twice returns `IsValid: true` from the validator both times (duplicate detection is Board Manager's concern, not the validator's)

### Experiential Criteria (Playtesting)

- [ ] Rejection feedback (reason text) appears within one frame of the submission action — no perceptible delay
- [ ] Players can read and understand the rejection reason without explanation — "Not a word" and "Need at least 2 letters" must parse immediately
- [ ] In a 10-player playtest, zero players report uncertainty about why their submission was rejected

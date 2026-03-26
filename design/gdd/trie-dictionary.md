# Trie Dictionary

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Strategic clarity — word validation is instantaneous and authoritative, so the player never waits and always trusts the result

---

## Overview

The Trie Dictionary is a pure C# class (no MonoBehaviour, no Unity dependencies) that loads a plaintext word list at application startup, inserts every word into a trie (prefix tree) data structure, and exposes a single `Contains(string word)` method returning a boolean in O(k) time where k is the length of the word being looked up. The word list is loaded from a Unity `TextAsset` (embedded in Resources). Because the trie is built once at startup and is thereafter read-only, there are no thread-safety concerns for lookup. This system is the sole source of truth for whether a string is a valid word; all other systems defer to it entirely.

---

## Player Fantasy

The player never experiences this system directly — it is invisible infrastructure. What it enables is the feeling of **immediate, trustworthy authority**: when the player submits a word, the result is instant. There is no perceptible delay, no loading spinner, no ambiguity. The player internalizes the dictionary as a fair referee: if they know the word is real, they trust it will be accepted; if it is rejected, they trust the word is not in the list. This reliability is the foundation of the game's first design pillar, Strategic Clarity. An unreliable or slow validator would undermine every other system that depends on it.

---

## Detailed Design

### Core Rules

1. The dictionary is represented as a trie where each node corresponds to one character in a word path, and a boolean flag `IsEndOfWord` marks terminal nodes.
2. At application startup, the `TrieDictionary` class reads a `TextAsset` named `WordList` from `Resources/Data/WordList.txt`. Each line in the file is one word. Lines are trimmed of whitespace and converted to uppercase before insertion.
3. Blank lines and lines containing characters outside A-Z after trimming are silently skipped during load. They are not inserted and do not cause errors.
4. Every word in the file is inserted into the trie by traversing from the root, creating child nodes as needed, and marking the final node `IsEndOfWord = true`.
5. `Contains(string word)` accepts any string. It converts the input to uppercase, traverses the trie character by character, and returns `true` only if the traversal reaches the end of the word AND the final node's `IsEndOfWord` flag is `true`.
6. If any character in the lookup word is not a child of the current node, `Contains` immediately returns `false`.
7. `Contains` never throws an exception. Null input, empty string input, and non-alpha input all return `false`.
8. The `TrieDictionary` class is instantiated once by the Word Validator. It exposes no static state, no singletons, and no global access. The Word Validator holds the single instance.
9. The `TrieDictionary` class provides a `WordCount` property (read-only integer) returning the number of words successfully inserted during the last `Load()` call.
10. The `TrieDictionary` class provides a `IsLoaded` property (read-only boolean) returning `true` only after `Load()` has completed without a missing-file error.
11. The word list file path within Resources is a constructor parameter (defaulting to `"Data/WordList"`), allowing tests to inject an alternate path with a controlled vocabulary.

### States and Transitions

This system has two states:

| State | Description | Transition In | Transition Out |
|-------|-------------|---------------|----------------|
| `Unloaded` | Instance created; trie is empty; `IsLoaded = false` | Object construction | `Load()` called |
| `Loaded` | Trie populated from file; `IsLoaded = true` | `Load()` completes | Object is discarded (no unload at runtime) |

There is no partial-load state. `Load()` is synchronous: it completes fully before returning. If the file is missing, the system remains in `Unloaded` state and logs an error (see Edge Cases). There is no retry or hot-reload at runtime.

### Interactions with Other Systems

**Word Validator (downstream consumer)**
- The Word Validator instantiates one `TrieDictionary`, calls `Load()` during its own `Awake()` or initialization, and then calls `Contains(word)` per submission.
- Data flow: Word Validator provides a string (the concatenated characters of selected reels, uppercase); Trie Dictionary returns a boolean.
- The Word Validator owns the `TrieDictionary` instance and is responsible for checking `IsLoaded` before forwarding player submissions. If `IsLoaded` is `false`, the Word Validator must treat all words as invalid and surface an error state to the Board Manager.
- The Trie Dictionary does not call back into the Word Validator; data flows one way.

---

## Formulas

### Data Schema

The word list file (`Resources/Data/WordList.txt`) conforms to this schema:

```
Field         : One word per line
Encoding      : UTF-8
Case          : Any (normalized to uppercase on load)
Valid chars   : A-Z only after normalization
Min word length: 2 (1-letter entries are inserted but the game rules
                    enforce a minimum selection of 2 reels — see Edge Cases)
Max word length: No enforced maximum; practically bounded by longest English
                 word (~45 characters for "pneumonoultramicroscopicsilicovolcanoconiosis")
Blank lines   : Skipped silently
Non-alpha lines: Skipped silently
```

### Trie Node Structure

Each node holds:

```
TrieNode {
  children : Dictionary<char, TrieNode>   // A-Z keys only
  IsEndOfWord : bool                      // true if a word ends here
}
```

### Insertion Complexity

- Time: O(k) where k = length of the word being inserted
- Space per node: O(1) amortized (dictionary resizes as needed)
- Total space: O(N x k_avg) where N = word count, k_avg = average word length

For a 10,000-word list with average length 6: approximately 60,000 node allocations in the worst case (no shared prefixes). In practice, English words share many prefixes, so actual node count will be significantly lower, estimated at 20,000-35,000 nodes for 10,000 words.

### Lookup Complexity

- Time: O(k) where k = length of the query string
- No allocation during lookup (traversal only, no string creation)

### Load Time Estimate

For a 10,000-word list on a mid-range mobile device (target platform lower bound):
- File read from Resources: ~5-15 ms
- String splitting and normalization: ~5-10 ms
- Trie insertion (10,000 words x avg 6 chars): ~10-20 ms
- **Total estimated load time: 20-45 ms**

This falls within the application startup sequence and is not perceptible to the player. No async loading is required. If the word list grows beyond 100,000 words, async loading should be reconsidered (estimated load time ~200-450 ms, which may cause a visible stutter if called after gameplay begins).

### Example Calculation

Query: `Contains("REEL")`

1. Start at root node
2. Look for child `R` — found, advance
3. Look for child `E` — found, advance
4. Look for child `E` — found, advance
5. Look for child `L` — found, advance; current node has `IsEndOfWord = true`
6. End of string reached with `IsEndOfWord = true` — return `true`

Query: `Contains("REE")`

1-4. Same as above through second `E`
5. End of string reached; current node has `IsEndOfWord = false` (because "REE" is not in the standard English word list)
6. Return `false`

---

## Edge Cases

**Missing word list file**
If `Resources/Data/WordList.txt` does not exist, `Load()` logs a Unity error: `"[TrieDictionary] Word list not found at Resources/{path}. Dictionary is empty."` The instance remains in `Unloaded` state (`IsLoaded = false`, `WordCount = 0`). All subsequent `Contains()` calls return `false`. The Word Validator detects `IsLoaded == false` and surfaces a critical error to the Board Manager. The game does not start a round with an unloaded dictionary.

**Empty word list file**
File exists but contains no valid words. `Load()` completes; `IsLoaded = true`; `WordCount = 0`. A Unity warning is logged: `"[TrieDictionary] Word list loaded with 0 words. Check file content."` All `Contains()` calls return `false`. The game is technically playable but no words will ever validate.

**Null input to Contains**
Returns `false` immediately without traversal. No exception is thrown.

**Empty string input to Contains**
Returns `false` immediately. An empty string cannot be a valid word.

**Input with non-alpha characters (spaces, digits, punctuation)**
After uppercasing, any character that is not A-Z will fail to find a child node on the first such character and return `false`.

**Duplicate words in the word list**
Inserting a word that already exists is a no-op (the path already exists and `IsEndOfWord` is already `true`). `WordCount` counts lines processed, not unique insertions, so duplicates increment the counter. This is acceptable — it has no effect on correctness.

**Very long query strings (overflow risk)**
Trie traversal is iterative, not recursive. There is no stack overflow risk regardless of query length. A 1,000-character query string simply traverses up to 1,000 nodes and returns `false` when no match is found.

**Single-character words in the word list**
Single-character entries ("A", "I") are valid per the word list standard and are inserted correctly. The game's minimum-reel-selection rule (enforced by the Board Manager) prevents single-character submissions from reaching the validator, so these entries have no gameplay effect but do not cause errors.

**Word list contains words longer than any possible reel combination**
Words longer than 6 characters are inserted into the trie but will never be returned by `Contains()` during a standard game session (maximum reel selection is 6). This wastes a small amount of memory but has no correctness impact. If the designer adds a 7+ reel configuration in the future, the dictionary already supports it.

---

## Dependencies

**Upstream (what this system depends on)**
- `Resources/Data/WordList.txt` — plaintext word list file. This is a content asset, not a code dependency. The word list must be present in the Unity project's Resources folder before a build is made. Source and curation of this file is outside the scope of this system (see Tuning Knobs).

**Downstream (what depends on this system)**
- **Word Validator** — the sole consumer. Holds the `TrieDictionary` instance, calls `Load()` at init, calls `Contains()` per word submission. No other system accesses the Trie Dictionary directly.

**No circular dependencies.** The Trie Dictionary has no knowledge of any other game system.

---

## Tuning Knobs

| Knob | Category | Current Value | Safe Range | What Changes at Extremes |
|------|----------|---------------|------------|--------------------------|
| Word list file path (constructor param) | Gate | `"Data/WordList"` | Any valid Resources-relative path | Wrong path = missing-file error on load; used to inject test vocabularies |
| Word list source | Content | SOWPODS (~267,000 words) | Any curated plaintext list | Too permissive risks obscure words feeling unfair; too restrictive risks common words being rejected and feeling punishing. SOWPODS chosen: largest standard Scrabble list, minimises "I know that word" rejections which are the most frustrating failure mode given fixed reel sequences |
| Minimum word length in list | Content | 2 (standard Scrabble) | 2-4 | Below 2: trivial combinations score; above 4: frustrating rejections for short valid words |

**Word List Source Recommendation**

The designer must select a word list before MVP. Three candidates:

| Source | Word Count | Notes |
|--------|-----------|-------|
| SOWPODS (Scrabble) | ~267,000 | Most permissive; includes obscure valid words; players rarely feel cheated |
| TWL06 (North American Scrabble) | ~178,000 | Slightly more conservative; well-known to word game players |
| Custom curated list | Designer-defined | Full control; highest effort; risk of missing common words |

**Recommended**: TWL06 or SOWPODS. The fixed reel sequences mean players will encounter specific letter combinations repeatedly; a larger dictionary reduces the frustration of knowing a word but having it rejected.

---

## Acceptance Criteria

### Functional Criteria (automated NUnit tests)

| # | Test | Pass Condition |
|---|------|---------------|
| AC-1 | Load with valid word list | `IsLoaded == true`, `WordCount > 0` after `Load()` |
| AC-2 | Contains returns true for known word | `Contains("WORD")` returns `true` when "WORD" is in the test list |
| AC-3 | Contains returns false for unknown word | `Contains("ZZZQ")` returns `false` when "ZZZQ" is not in the test list |
| AC-4 | Contains returns false for prefix only | `Contains("CAT")` returns `false` when list contains only "CATS" (not "CAT") |
| AC-5 | Contains returns false for null | `Contains(null)` returns `false` and does not throw |
| AC-6 | Contains returns false for empty string | `Contains("")` returns `false` and does not throw |
| AC-7 | Contains is case-insensitive | `Contains("word")` and `Contains("WORD")` return the same result |
| AC-8 | Contains returns false for non-alpha input | `Contains("W0RD")` returns `false` |
| AC-9 | Missing file leaves IsLoaded false | After `Load()` with a nonexistent path, `IsLoaded == false` and `Contains("ANY")` returns `false` |
| AC-10 | WordCount reflects inserted word count | For a test list with 5 known words, `WordCount == 5` after load |
| AC-11 | Duplicate insertion does not corrupt | List with "CAT" twice; `Contains("CAT")` returns `true`; no exception |
| AC-12 | Lookup speed: 1,000 queries under 10 ms | Run 1,000 `Contains()` calls on a loaded 10,000-word dictionary; total elapsed time < 10 ms |

### Experiential Criteria (verified by hand during integration testing)

| # | Criterion | How to Verify |
|---|-----------|---------------|
| AC-13 | No perceptible load delay | Start a game session; no visible stutter or loading screen attributable to dictionary load |
| AC-14 | Common words accepted | Submit "THE", "AND", "WORD", "PLAY", "GAME" — all accepted |
| AC-15 | Nonsense strings rejected | Submit "XQZP", "AAAA", "BBBBB" — all rejected |
| AC-16 | Rejection is immediate | No perceptible delay between submission and valid/invalid feedback; measured < 1 frame at 60fps |

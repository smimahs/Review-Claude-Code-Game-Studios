# Reel Sequence Data

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Replayability through determinism — fixed, known sequences let skilled players plan ahead and optimize across multiple sessions

---

## Overview

Reel Sequence Data is a Unity ScriptableObject that defines the complete fixed character sequence for each of the six reels on the board. Each reel holds an ordered list of characters (its "tape") and starts at index 0. When a reel advances, its index increments by one. When the index reaches the end of the tape, it wraps to the back to index 0. The sequences are authored by the designer and baked into the asset; they never randomize. A single `ReelSequenceData` ScriptableObject asset contains the data for all six reels, making the entire board layout auditable and reproducible from one file. The sequences shipped with the game are designed so that common English letters appear frequently and high-value letters appear sparingly, creating a natural distribution that rewards strategic reel management.

---

## Player Fantasy

The player should feel like they are **reading a puzzle**, not spinning a lottery. Because the sequences never randomize, a player who has seen reels 1 and 2 advance three times in a single session can predict what characters are coming. This creates the sensation of a skilled craftsperson who sees potential combinations that a casual player misses. Over multiple sessions with the same puzzle configuration, the player feels their knowledge accumulating — they remember that Reel 3 reaches a Q two steps after a common E, and they build plays around that knowledge. The determinism is the mastery. The satisfaction is not "I got lucky" but "I saw it coming."

---

## Detailed Design

### Core Rules

1. The `ReelSequenceData` ScriptableObject contains exactly six `ReelDefinition` entries, one per reel. The array is indexed 0-5, corresponding to reels left-to-right on the board (Reel 0 = leftmost, Reel 5 = rightmost).
2. Each `ReelDefinition` contains an ordered `char[]` (the tape) and a `string reelId` for debugging purposes.
3. All characters in every tape must be uppercase Latin letters A-Z. No digits, spaces, punctuation, or non-Latin characters are permitted. The ScriptableObject validates this constraint in a custom `OnValidate()` method in the Unity Editor; invalid characters cause an editor warning.
4. Each tape must contain at least 4 characters and at most 52 characters. The recommended length is 12-26 characters (see Tuning Knobs). Tapes of different lengths are allowed; the six reels do not need matching tape lengths.
5. At runtime, the Reel Controller reads its assigned `ReelDefinition` from this asset. It does not copy the array — it references the ScriptableObject. The ScriptableObject is read-only at runtime; no system writes to it.
6. The Reel Controller is responsible for maintaining the current index (mutable state). The ScriptableObject is pure data.
7. When a reel advances past the last character in its tape (index == tape.Length - 1), the next advance wraps to index 0. Wrap-around is a normal game event, not an error. The Board UI must communicate wrap-around visually (design responsibility of the Board UI system).
8. The characters visible at the start of a session are always index 0 for every reel. This is the defined starting state of every game.
9. No two adjacent reels (reels N and N+1) may have identical tapes. This prevents the degenerate board state where a player can always build a word by selecting two consecutive reels with the same letters.
10. The six reel sequences are authored independently. No procedural generation or mutation occurs at runtime.

### States and Transitions

This system is a pure data asset. It has no runtime state of its own. The mutable state (current index per reel) lives in the Reel Controller, not here.

| State | Owner | Notes |
|-------|-------|-------|
| Static data | ScriptableObject asset | Set at design time; never mutated at runtime |
| Current index | Reel Controller (one per reel) | Initialized to 0 at game start; incremented on advance |

### Interactions with Other Systems

**Reel Controller (downstream consumer)**
- At initialization, each Reel Controller receives a reference to its `ReelDefinition` (passed in by the Board Manager which holds the `ReelSequenceData` asset reference).
- Data flow: Reel Controller reads `ReelDefinition.tape[currentIndex]` to get the currently displayed character. On `Advance()`, the Reel Controller increments its index (with wrap-around) and reads the new character.
- The Reel Controller does not write to the ScriptableObject. The ScriptableObject does not call into the Reel Controller.
- The Board Manager holds the `ReelSequenceData` asset reference (assigned in the Inspector) and distributes `ReelDefinition` entries to each Reel Controller during scene setup.

**Board UI (indirect consumer)**
- The Board UI reads the current character from the Reel Controller, not directly from this asset. However, for a "preview next character" feature (Post-MVP), the Board UI may read `tape[(currentIndex + 1) % tape.Length]` via the Reel Controller's exposed `PeekNext()` method. The Board UI never accesses the ScriptableObject directly.

---

## Formulas

### Data Schema

```
ReelSequenceData : ScriptableObject
  reels : ReelDefinition[6]   // exactly 6 entries, indexed 0-5

ReelDefinition
  reelId  : string            // e.g. "Reel_0", "Reel_1", ... for debugging
  tape    : char[]            // ordered character sequence, min 4, max 52, recommended 12-26
```

### Index Wrap Formula

```
nextIndex = (currentIndex + 1) % tape.Length
```

Where:
- `currentIndex`: integer in range [0, tape.Length - 1]
- `tape.Length`: integer in range [4, 52]
- `nextIndex`: result in range [0, tape.Length - 1]

Example: tape length = 16, currentIndex = 15
`nextIndex = (15 + 1) % 16 = 0`  (wraps to start)

### Character Distribution Guidelines

When authoring sequences, aim for the following rough letter frequency targets per tape (based on English letter frequency, adjusted for gameplay):

| Frequency Class | Letters | Target share of tape |
|----------------|---------|---------------------|
| Very Common | E, T, A, O, I, N, S, R | 50-60% of tape characters |
| Common | H, L, D, C, U, M, F, P | 25-35% of tape characters |
| Uncommon | G, W, Y, B, V, K | 10-15% of tape characters |
| Rare | J, X, Q, Z | 0-5% of tape characters (0-1 appearances per tape) |

This distribution ensures the board is usually in a state where words are formable, while rare letters create high-value moments without dominating play.

### Shipped Reel Sequences (v1.0)

The following sequences are the designed starting configuration. Each sequence is 16 characters long, providing a full cycle visible in approximately 8-16 valid word submissions for an average player.

| Reel | ID | Tape (left to right = index 0 to 15) | Design Notes |
|------|----|--------------------------------------|--------------|
| 0 | Reel_0 | S, T, A, R, E, D, I, N, G, O, U, L, C, A, P, E | Opens on S (strong word-starter); cycles through common consonants and vowels; C and P create variety mid-cycle |
| 1 | Reel_1 | H, E, A, T, I, N, G, L, Y, O, U, R, S, W, A, N | Opens on H; heavy vowel representation (E,A,I,O,U appear across cycle); W and Y provide uncommon variation |
| 2 | Reel_2 | A, R, T, I, S, T, O, N, E, D, B, L, U, F, F, Y | Opens on A (strong vowel start); B and F cluster creates mid-cycle consonant challenge; ends on Y |
| 3 | Reel_3 | N, O, B, L, E, S, T, R, A, I, N, G, U, M, P, H | Opens on N; NOBLE prefix opportunity at index 0; M and P near end of cycle; ends on H |
| 4 | Reel_4 | I, N, D, U, S, T, R, Y, A, L, E, C, O, V, E, R | Opens on I; INDUSTRY root across indices 0-6; V provides high-value uncommon option |
| 5 | Reel_5 | O, U, T, W, A, R, D, S, L, Y, C, H, E, A, P, N | Opens on O; OUTWARDS prefix at indices 0-6; C and H in mid-cycle create CH digraph opportunity; ends on N |

**Rationale for these sequences:**

- Each tape opens on a character that frequently begins or ends English words (S, H, A, N, I, O), giving the player immediately playable options on turn 1.
- No two adjacent reels share an opening character, preventing trivial same-letter pairs on the starting board.
- Each tape contains 6-8 vowels across its 16 characters (37-50%), consistent with English text frequency (~38% vowels).
- The sequences are designed so that roughly every 4-6 advances, the player encounters a new vowel or consonant cluster, creating natural rhythm of opportunity.
- High-value letters (J=8, Q=10, X=8, Z=10) are intentionally absent from the v1.0 sequences. Their inclusion is a Post-MVP tuning decision (see Tuning Knobs).

---

## Edge Cases

**Reel reaches end of tape (wrap-around)**
When `currentIndex == tape.Length - 1` and `Advance()` is called, the index becomes 0. The character displayed is `tape[0]`. This is expected behavior. The Board UI is responsible for communicating this wrap visually (e.g., a brief animation or indicator). The Reel Controller does not treat this differently from any other advance.

**Player exhausts all turns before any reel wraps**
In a standard 20-turn game, a reel can advance at most 20 times. With a 16-character tape, this means at most one full wrap. With a 24-character tape, wrapping may never occur in a session. This is intentional: longer tapes create more novel board states per session.

**All reels wrap simultaneously**
If the player is skilled enough to advance all six reels to their wrap point at the same turn, all six wrap back to index 0. This is not an error; it is a valid (and impressive) game state. The board returns to its starting configuration.

**ScriptableObject asset not assigned in Inspector**
If the Board Manager's `ReelSequenceData` reference is null at startup, the Board Manager logs a Unity error: `"[BoardManager] ReelSequenceData asset not assigned. Board cannot initialize."` The game does not start a round. This is a developer error (missing Inspector assignment), not a runtime error from player action.

**Tape has fewer than 4 characters**
Blocked at edit time by `OnValidate()`. If somehow bypassed (e.g., by directly editing the serialized asset file), the Reel Controller will still function correctly, but tape variety will be severely limited. This would surface during QA.

**Tape contains non-A-Z characters**
Blocked at edit time by `OnValidate()` with a Unity Editor warning. If a non-A-Z character appears in the tape at runtime (e.g., due to asset corruption), the Reel Controller logs an error when that character becomes the current display character. The character is displayed as `?` and does not participate in word validation (the Word Validator only accepts A-Z inputs).

**Two adjacent reels receive identical tape assignments**
The `OnValidate()` method checks for adjacent identical tapes and logs a Unity Editor warning. This is not blocked (the designer may have a deliberate reason), but it is flagged.

---

## Dependencies

**Upstream (what this system depends on)**
- None. The ScriptableObject is pure authored data. It has no runtime code dependencies.
- The Unity Editor's `OnValidate()` callback is used for edit-time validation only; this is not a runtime dependency.

**Downstream (what depends on this system)**
- **Board Manager** — holds the asset reference (assigned in Inspector). Distributes `ReelDefinition` entries to Reel Controllers at scene initialization. The Board Manager is the only system that holds a direct reference to the `ReelSequenceData` asset; all other systems receive their data indirectly through the Reel Controller.
- **Reel Controller** — receives its `ReelDefinition` from the Board Manager. Reads `tape[currentIndex]` on every display update and every `Advance()` call.

**No circular dependencies.** This asset has no knowledge of any system that uses it.

---

## Tuning Knobs

| Knob | Category | Current Value | Safe Range | What Changes at Extremes |
|------|----------|---------------|------------|--------------------------|
| Tape length (per reel) | Content | 16 characters | 4-52 | Too short (4-6): reels wrap frequently, board cycles quickly, late-game feels repetitive; too long (40+): reel never wraps within a session, reducing the "board transformation" progression feel |
| Number of vowels per tape | Content | 6-8 out of 16 (~40-50%) | 4-10 out of 16 | Too few vowels (< 4): word formation becomes very difficult, frustrating; too many (> 10): trivial vowel-heavy words dominate, low strategic depth |
| High-value letter inclusion (J, Q, X, Z) | Content | Absent in v1.0 | 0-1 per tape | Including high-value letters creates exciting score spikes but reduces word formation reliability; recommend introducing in a second puzzle configuration after base game is validated |
| Starting character per reel | Content | See shipped sequences above | Any A-Z | Starting on a vowel is more forgiving for new players; starting on a consonant rewards players who understand combination planning |
| Number of distinct reel configurations (puzzle sets) | Gate | 1 (the shipped sequences) | 1-N | One configuration for MVP; multiple configurations are the primary Post-MVP replayability lever |

---

## Acceptance Criteria

### Functional Criteria (automated NUnit tests, via test harness that instantiates mock ReelDefinitions)

| # | Test | Pass Condition |
|---|------|---------------|
| AC-1 | Asset has exactly 6 reels | `reelSequenceData.reels.Length == 6` |
| AC-2 | No tape is null or empty | All 6 `ReelDefinition.tape` arrays are non-null and have length >= 4 |
| AC-3 | All characters are A-Z | For every character in every tape, `char >= 'A' && char <= 'Z'` |
| AC-4 | Index wrap formula is correct | Given tape length 5 and currentIndex 4, `(4+1) % 5 == 0` |
| AC-5 | Advance increments index | After one `Advance()`, Reel Controller index == 1 (starting from 0) |
| AC-6 | Advance wraps at end | After `tape.Length` advances from index 0, Reel Controller index == 0 |
| AC-7 | Starting state is always index 0 | At game start, all 6 Reel Controllers report `CurrentIndex == 0` |
| AC-8 | ScriptableObject is read-only at runtime | No system modifies the tape array after load; confirmed by code review |

### Experiential Criteria (verified by playtesting)

| # | Criterion | How to Verify |
|---|-----------|---------------|
| AC-9 | Board is never "stuck" at start | On a fresh game, a valid word can be formed within the first 3 turns without any reel advancing (using only starting characters) |
| AC-10 | Board evolves visibly after 5 turns | After 5 valid word submissions, at least 3 reels are displaying characters different from their starting state |
| AC-11 | Wrap-around is communicated | When any reel wraps back to index 0, a playtester notices the visual change without being told to look for it |
| AC-12 | No two adjacent reels feel "the same" | A playtester asked to describe each reel can distinguish all 6 as having different "feel" after 10 minutes of play |
| AC-13 | High-value words exist in the cycle | In a full 20-turn game, a player who plays optimally can find at least one word scoring 15+ points |

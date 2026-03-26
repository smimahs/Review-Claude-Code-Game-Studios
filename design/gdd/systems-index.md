# ReelWords — Systems Index

**Created**: 2026-03-26
**Last Updated**: 2026-03-26
**Total Systems**: 21 (17 MVP, 4 Post-MVP)

---

## Systems Enumeration

### Gameplay

| # | System | Layer | Description | Source |
|---|--------|-------|-------------|--------|
| 1 | Trie Dictionary | Foundation | Pure C# class: loads word list, inserts all words into a Trie, exposes O(k) `Contains(word)` lookup | Explicit |
| 2 | Reel Sequence Data | Foundation | Data asset defining the fixed character sequence for each of the 6 reels | Explicit |
| 3 | Letter Value Table | Foundation | Data asset mapping each letter to its Scrabble-style point value | Explicit |
| 4 | Reel Controller | Core | Manages one reel's character sequence, current index, and `Advance()` logic | Explicit |
| 5 | Word Validator | Core | Reads selected reel characters, queries Trie, returns valid/invalid + reason | Implicit |
| 6 | Scoring System | Core | Calculates word score from letter values; tracks session total | Explicit |
| 7 | Board Manager | Feature | Owns all 6 reels, tracks which reels are selected for the current word, coordinates submission flow | Explicit |
| 8 | Turn Manager | Feature | Tracks turns remaining; consumes a turn on every submission (valid or invalid); fires `OnGameOver` when exhausted | Explicit |
| 9 | Timer System | Feature | Countdown timer for timer mode; pause/resume; bonus-time hook on valid word submission | Explicit |
| 10 | Game Mode Manager | Feature | Selects and configures the active game mode (turn-limit vs timer); wires the correct win/lose conditions | Implicit |

### UI / Presentation

| # | System | Layer | Description | Source |
|---|--------|-------|-------------|--------|
| 11 | Board UI | Presentation | Renders 6 reels, highlights selected characters, shows word being composed | Implicit |
| 12 | Word Input UI | Presentation | Submit/Clear controls; displays the word currently being composed | Implicit |
| 13 | Score UI | Presentation | Displays current score, word-score pop-up on submission, session total | Implicit |
| 14 | Turn/Timer HUD | Presentation | Shows turns remaining (turn-limit mode) or countdown timer (timer mode) | Implicit |
| 15 | Main Menu | Presentation | Mode selection, start game, high-scores entry point | Explicit |
| 16 | Game Over Screen | Presentation | Final score display, restart and main-menu options | Explicit |
| 17 | Pause Menu | Presentation | Pause overlay during play; resume, quit to menu | Implicit |

### Infrastructure

| # | System | Layer | Description | Source |
|---|--------|-------|-------------|--------|
| 18 | Game State Machine | Infrastructure | Top-level states: MainMenu → Playing → Paused → GameOver; routes transitions | Implicit |
| 19 | Scene Flow | Infrastructure | Loads/unloads scenes and manages transitions in response to state changes | Implicit |
| 20 | High Score / Save | Polish | Persists local high scores via JSON; loaded by menu and game-over screen | Implicit |
| 21 | Audio Manager | Polish | SFX playback (reel advance, word found, invalid, timer tick); wraps AudioRandomContainer | Implicit |

---

## Dependency Map

| System | Depends On | Depended On By |
|--------|-----------|----------------|
| Trie Dictionary | — | Word Validator |
| Reel Sequence Data | — | Reel Controller |
| Letter Value Table | — | Scoring System |
| Reel Controller | Reel Sequence Data | Board Manager, Board UI |
| Word Validator | Trie Dictionary | Board Manager |
| Scoring System | Letter Value Table | Board Manager, Score UI, Game Over Screen |
| Board Manager | Reel Controller, Word Validator, Scoring System | Turn Manager, Timer System, Game Mode Manager, Board UI, Word Input UI |
| Turn Manager | Board Manager | Game Mode Manager, Turn/Timer HUD |
| Timer System | Board Manager | Game Mode Manager, Turn/Timer HUD, Pause Menu |
| Game Mode Manager | Turn Manager, Timer System, Board Manager | Game State Machine |
| Game State Machine | Game Mode Manager | Scene Flow, Main Menu, Game Over Screen, Pause Menu |
| Scene Flow | Game State Machine | — |
| Board UI | Board Manager, Reel Controller | — |
| Word Input UI | Board Manager | — |
| Score UI | Scoring System | — |
| Turn/Timer HUD | Turn Manager, Timer System | — |
| Main Menu | Game State Machine | — |
| Game Over Screen | Game State Machine, Scoring System | — |
| Pause Menu | Game State Machine, Timer System | — |
| High Score / Save | — | Main Menu, Game Over Screen |
| Audio Manager | — (event-driven) | — |

### Bottleneck Systems (High Risk)

- **Board Manager** — 5 systems depend on it directly. Interface changes cascade to Board UI, Word Input UI, Turn Manager, Timer System, and Game Mode Manager. Design this carefully.
- **Game State Machine** — all 5 screen/UI systems depend on it. Must be designed before any UI work begins.

---

## Priority Tiers

### MVP (17 systems)
Required for a complete playable first build.

Trie Dictionary, Reel Sequence Data, Letter Value Table, Reel Controller, Word Validator, Scoring System, Board Manager, Turn Manager, Game Mode Manager, Game State Machine, Scene Flow, Board UI, Word Input UI, Score UI, Turn/Timer HUD, Main Menu, Game Over Screen

### Post-MVP (4 systems)
Second mode, persistence, and polish.

Timer System, High Score/Save, Pause Menu, Audio Manager

---

## Recommended Design Order

Design follows MVP priority first, then dependency order within each tier.

| Order | System | Priority | Layer | GDD Status |
|-------|--------|----------|-------|------------|
| 1 | Trie Dictionary | MVP | Foundation | Designed |
| 2 | Reel Sequence Data | MVP | Foundation | Designed |
| 3 | Letter Value Table | MVP | Foundation | Designed |
| 4 | Reel Controller | MVP | Core | Designed |
| 5 | Word Validator | MVP | Core | Designed |
| 6 | Scoring System | MVP | Core | Designed |
| 7 | Board Manager | MVP | Feature | Approved |
| 8 | Turn Manager | MVP | Feature | Approved |
| 9 | Game Mode Manager | MVP | Feature | Approved |
| 10 | Game State Machine | MVP | Infrastructure | Approved |
| 11 | Scene Flow | MVP | Infrastructure | Approved |
| 12 | Board UI | MVP | Presentation | Designed |
| 13 | Word Input UI | MVP | Presentation | Designed |
| 14 | Score UI | MVP | Presentation | Designed |
| 15 | Turn/Timer HUD | MVP | Presentation | Designed |
| 16 | Main Menu | MVP | Presentation | Designed |
| 17 | Game Over Screen | MVP | Presentation | Designed |
| 18 | Timer System | Post-MVP | Feature | Designed |
| 19 | High Score / Save | Post-MVP | Polish | Designed |
| 20 | Pause Menu | Post-MVP | Presentation | Designed |
| 21 | Audio Manager | Post-MVP | Polish | Designed |

---

## Progress Tracker

- **Designed**: 21 / 21
- **MVP Designed**: 17 / 17
- **Post-MVP Designed**: 4 / 4

*Update this tracker as GDDs are completed via `/design-system`.*

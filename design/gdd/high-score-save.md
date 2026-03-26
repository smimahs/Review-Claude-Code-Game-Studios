# High Score / Save System

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Replayability Through Determinism (persistent scores give skilled players a concrete record of mastery and a target to beat)

---

## Overview

The High Score / Save System is a pure persistence layer with no gameplay logic.
It loads the top N high scores per game mode from a local JSON file at application
start, makes them available to the Main Menu and Game Over Screen via a simple query
API, and appends/sorts/trims the score list and saves it back to disk at game over.
One JSON file stores scores for both game modes (turn-limit and timer) in separate
arrays. The system handles file corruption by resetting to an empty list with a
warning log rather than crashing. All I/O uses `System.IO.File` and `Newtonsoft.Json`
(Unity's built-in `JsonUtility` lacks support for List serialization without a wrapper
class, making `Newtonsoft.Json` the cleaner choice for this structure).

---

## Player Fantasy

High scores in a deterministic puzzle game serve a specific psychological function:
they are a **skill ledger**. Because reel sequences are fixed, players can return to
the same board state and attempt to surpass their previous best through improved word
recognition and sequence planning — not through luck. The moment the player sees their
new score appear in the top-10 list (or displace their own previous record) should
feel like measurable proof of growth. The target MDA aesthetics are **Challenge**
(beating a personal record is a form of self-competition) and **Discovery** (a new
personal best reveals that the player's vocabulary or strategy has expanded).

---

## Detailed Design

### Core Rules

1. The system maintains two independent score lists: one for `TurnLimit` mode and one
   for `Timer` mode. Each list holds at most `MaxScoresPerMode` entries (default: 10).
2. The system loads from disk exactly once at application startup, before any scene
   is loaded. Loading is asynchronous (coroutine or async/await); the Main Menu waits
   for load completion before displaying the leaderboard UI.
3. Saving is synchronous and occurs once at game over, after the final score is
   computed. The file is small enough (~2 KB at maximum capacity) that a synchronous
   write on the main thread is acceptable and simplifies error handling.
4. A new entry is always inserted in sorted order (descending by `Score`). Entries
   with identical scores are ordered by submission time (newer entries appear higher
   than equal-score entries — tiebreak favors the most recent achievement to encourage
   continued play).
5. After insertion, if the list exceeds `MaxScoresPerMode`, the last entry (lowest
   score) is removed.
6. The system provides a read-only query method `GetScores(GameMode mode)` returning
   an immutable copy of the list for that mode.
7. The system provides a `IsNewHighScore(int score, GameMode mode)` query that returns
   `true` if `score` would appear in the top-N list — used by the Game Over Screen to
   display a "New High Score!" banner before saving.
8. If the JSON file does not exist on load, this is treated identically to an empty
   file: initialize empty lists and proceed normally. The file is created on the first
   save.

### States and Transitions

The system is stateless at runtime (no running state machine). Its lifecycle is:

```
App Start
    │
    ▼
Load() ──► [file exists?] ──No──► Initialize empty lists
               │
              Yes
               ▼
           ParseJSON ──► [parse succeeds?] ──No──► Log warning, initialize empty lists
                                │
                               Yes
                                ▼
                         Lists available to callers
                                │
                    Game Over triggers Save(entry)
                                │
                                ▼
                         Insert → Sort → Trim
                                │
                                ▼
                        SerializeJSON → WriteFile
```

### Interactions with Other Systems

**Main Menu**: Calls `HighScoreService.GetScores(GameMode)` to populate the
leaderboard display. Must wait for the async load to complete before querying —
use the `HighScoreService.IsLoaded` boolean property or subscribe to
`OnLoadComplete` event.

**Game Over Screen**: Calls `HighScoreService.IsNewHighScore(score, mode)` to
determine whether to show the "New High Score!" banner. Then calls
`HighScoreService.Save(HighScoreEntry)` to persist the result. Displays the
updated leaderboard after save.

**Game Mode Manager**: Provides the `GameMode` enum value included in each
`HighScoreEntry`. The save system does not read game state directly.

**Scoring System**: The final score integer is passed to the Game Over Screen by
the Game State Machine or Scoring System. The save system accepts the value; it
does not read from Scoring System directly.

---

## Formulas

### Entry Insertion

```
function TryInsert(entry: HighScoreEntry, list: List<HighScoreEntry>):
    list.Add(entry)
    list.SortDescending(by: Score, tiebreak: newer DateAdded first)
    if list.Count > MaxScoresPerMode:
        list.RemoveAt(list.Count - 1)
    return list
```

### Minimum Qualifying Score

To determine whether a score qualifies for the list without inserting it:

```
function IsNewHighScore(score: int, mode: GameMode) → bool:
    list = GetScores(mode)
    if list.Count < MaxScoresPerMode: return true
    return score >= list[list.Count - 1].Score
```

Note: `>=` is intentional. A score that ties the last entry qualifies (it will
displace the older entry with an equal score due to the tiebreak rule).

### File Size Estimation

Maximum file size at `MaxScoresPerMode = 10`, two modes, no compression:
- Entry fields: `score` (int), `wordsFound` (int), `mode` (string ~10 chars),
  `date` (ISO 8601 string ~24 chars)
- Per-entry JSON overhead: ~60 bytes
- Per-entry total: ~110 bytes
- 20 entries total: ~2.2 KB

This is well within the synchronous-write threshold. No streaming required.

### Example Entry JSON

```json
{
  "TurnLimit": [
    { "score": 847, "wordsFound": 22, "mode": "TurnLimit", "date": "2026-03-26T14:32:01Z" },
    { "score": 712, "wordsFound": 19, "mode": "TurnLimit", "date": "2026-03-25T09:15:44Z" }
  ],
  "Timer": [
    { "score": 634, "wordsFound": 18, "mode": "Timer", "date": "2026-03-26T15:00:12Z" }
  ]
}
```

---

## Edge Cases

**File does not exist (first launch)**: `File.Exists()` returns false. Initialize
both lists as empty `List<HighScoreEntry>`. The file will be created on first save.
No warning is logged — this is the normal first-run state.

**File exists but is empty**: `File.ReadAllText()` returns `""`. `JsonConvert.DeserializeObject`
returns null or throws. Catch the exception, log a warning ("High score file was empty;
starting fresh"), initialize empty lists.

**File is corrupted** (invalid JSON): `JsonConvert.DeserializeObject` throws
`JsonException`. Catch it, log a warning with the exception message, initialize
empty lists. Do not delete or overwrite the corrupted file until the next successful
save — this preserves the user's corrupted data for debugging.

**Partial corruption** (one mode array valid, one not): Validate each mode array
independently. If `TurnLimit` array parses successfully and `Timer` array does not,
retain the valid list and initialize the invalid one as empty. Log a warning for
the affected mode only.

**Score of zero**: Valid. A game where no valid words were found produces a score of
zero. Zero-score entries are inserted normally and may appear in the leaderboard if
fewer than `MaxScoresPerMode` entries exist.

**Negative score**: Not possible under the current scoring rules (letter values are
all positive integers). If a future system introduces penalties, `score` type remains
`int` and negative scores are sorted lower than zero — no code change needed.

**Save called before load completes**: If the game somehow reaches Game Over before
the async load finishes (e.g., extremely fast game on a slow disk), the system must
queue the save operation and execute it immediately after load completes. Implement
with a pending-save flag checked in the `OnLoadComplete` callback.

**Multiple saves in one session**: Not expected under normal flow, but if `Save()` is
called twice (e.g., a bug causes two `OnGameOver` events), the second save should be
a no-op if the same entry has already been inserted. Guard with an `_savedThisSession`
bool that resets on `StartTimer()` / new session start.

**Disk full or write failure**: `File.WriteAllText` throws `IOException`. Catch it,
log an error, and surface a user-facing message: "Could not save high score (disk
full or write error)." Do not crash. The score is lost but the game continues normally.

**`MaxScoresPerMode` reduced in a future update**: If the stored file has more entries
than the current `MaxScoresPerMode`, trim the loaded list to the current maximum after
parsing. The trimmed entries are lost on next save. Log a debug message noting the trim.

---

## Dependencies

### This system requires from others

| System | What it needs |
|--------|--------------|
| Game State Machine | Signals when a game session has concluded so the save flow begins |
| Game Over Screen | Passes the completed `HighScoreEntry` struct (score, wordsFound, mode, date) to `Save()` |
| Unity Runtime | `Application.persistentDataPath` for the storage path; `System.IO.File` for read/write |
| Newtonsoft.Json | JSON serialization/deserialization (`com.unity.nuget.newtonsoft-json` package) |

### This system provides to others

| System | What it provides |
|--------|-----------------|
| Main Menu | `GetScores(GameMode) → IReadOnlyList<HighScoreEntry>` |
| Main Menu | `OnLoadComplete` event (subscribe before querying) |
| Main Menu | `IsLoaded` (bool property) |
| Game Over Screen | `IsNewHighScore(int score, GameMode mode) → bool` |
| Game Over Screen | `Save(HighScoreEntry entry)` |
| Game Over Screen | `GetScores(GameMode)` (to display updated list after save) |

---

## Tuning Knobs

All values live in `assets/data/high-score-config.json` or a ScriptableObject at
`assets/data/HighScoreConfig.asset`.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `MaxScoresPerMode` | Gate | 10 | 5 – 25 | Number of entries retained per mode. Lower = leaderboard feels exclusive; higher = more players see themselves on the list. 10 is a convention from arcade games. |
| `StorageFileName` | Gate | `"highscores.json"` | Any valid filename | Name of the persisted file under `Application.persistentDataPath`. |

**Storage path (not a tuning knob, but documented for implementers):**

```
Application.persistentDataPath + "/" + StorageFileName
```

On Windows: `%APPDATA%/../LocalLow/<CompanyName>/<ProductName>/highscores.json`
On macOS: `~/Library/Application Support/<CompanyName>/<ProductName>/highscores.json`

---

## Acceptance Criteria

### Functional

- [ ] On first launch (no file), `GetScores()` returns an empty list for both modes without error.
- [ ] After a game over, `Save()` writes a valid JSON file to `Application.persistentDataPath`.
- [ ] On subsequent launch, `GetScores()` returns the previously saved entries in descending score order.
- [ ] Only the top `MaxScoresPerMode` entries are retained; the (N+1)th lowest is removed.
- [ ] Two entries with identical scores: the newer entry appears higher in the list.
- [ ] `IsNewHighScore(score, mode)` returns `true` when the list has fewer than `MaxScoresPerMode` entries, regardless of score value.
- [ ] `IsNewHighScore(score, mode)` returns `true` when `score` ties or exceeds the last entry.
- [ ] `IsNewHighScore(score, mode)` returns `false` when `score` is strictly less than the last entry and the list is full.
- [ ] A corrupted JSON file is handled without a crash; empty lists are initialized; a warning is logged.
- [ ] Scores from `TurnLimit` mode do not appear in `Timer` mode's list and vice versa.
- [ ] `Save()` called twice in one session with the same data inserts only one entry.
- [ ] Disk write failure logs an error and does not crash the application.

### Experiential (Playtest Validation)

- [ ] The Game Over Screen displays "New High Score!" on the first game session (empty leaderboard always qualifies).
- [ ] On a subsequent session with a lower score, "New High Score!" does not appear.
- [ ] The leaderboard on the Main Menu displays in less than 100ms after the menu loads (load performance check).
- [ ] Player can identify their rank among the 10 entries without confusion (UI legibility check — out of scope for this GDD, but acceptance must be verified end-to-end).

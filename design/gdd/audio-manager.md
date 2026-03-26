# Audio Manager

> **Status**: Designed
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-03-26
> **Implements Pillar**: Satisfying Progression (audio feedback makes every word submission and every reel advance feel tactile and consequential)

---

## Overview

The Audio Manager is an event-driven, stateless service that maps game events to
SFX playback. It subscribes to events from Board Manager, Turn Manager, Timer System,
and Game Mode Manager at initialization, then plays appropriate sounds through
category-specific `AudioSource` components when those events fire. Every sound
category uses Unity 6's `AudioRandomContainer` resource assigned to the
`AudioSource.resource` property, providing 2-4 variants per sound to prevent
listener fatigue. The Audio Manager has no direct knowledge of game state — it
reacts to events and plays sounds. It does not manage background music; this project
has SFX only. A per-category `AudioSource` architecture (not one source per event
call) allows mixing control and prevents simultaneous identical sounds from
collapsing into a single inaudible event.

---

## Player Fantasy

Sound is the most immediate feedback channel in ReelWords. The satisfying "click" of
a reel advancing, the rewarding chime of a valid word, and the tense metronomic tick
of a low timer are not decorative — they are the primary sensory confirmation that
the player's actions are having effect. The audio design targets the **Sensation**
aesthetic (MDA): the physical pleasure of hearing a well-chosen word land. Variation
through `AudioRandomContainer` prevents any single sound from becoming grating across
a long session, supporting the **Submission** aesthetic (player comfortably enters a
flow state without being yanked out by a repetitive sound).

---

## Detailed Design

### Core Rules

1. The Audio Manager is a singleton service instantiated once at application start
   and persisted across scene loads (`DontDestroyOnLoad`). It re-subscribes to events
   after each scene load since event sources (Board Manager, etc.) are re-instantiated
   per scene.
2. Each sound category is assigned exactly one `AudioSource` component on the Audio
   Manager `GameObject`. Playing a new sound on a category source that is already
   playing calls `AudioSource.Play()` which restarts it (not `PlayOneShot`). This
   prevents event spam from creating overlapping identical sounds for categories where
   overlap is undesirable (e.g., `invalid_word`). See rule 4 for exceptions.
3. `AudioSource.resource` is set to the appropriate `AudioRandomContainer` asset.
   Unity 6 selects a random variant from the container each time `Play()` is called,
   providing automatic variation.
4. The `reel_advance` category uses `PlayOneShot` instead of `Play()`, because each
   reel advance is a discrete physical event that should be heard fully even when
   multiple reels advance in rapid succession (e.g., a 4-letter word advances 4 reels
   nearly simultaneously). `PlayOneShot` on a single `AudioSource` supports natural
   overlap for this category.
5. The `tick` loop (low timer warning) is handled differently from one-shot sounds:
   when `OnLowTime` fires, the tick `AudioSource` starts looping (`AudioSource.loop = true`,
   then `Play()`). It continues looping until `OnTimerExpired` fires or the game
   transitions away from `Playing`, at which point it is explicitly stopped.
6. All `AudioSource` components are set to 2D spatial blend (no 3D positioning).
   ReelWords has no spatial audio requirements.
7. If an event fires when the Audio Manager is not fully initialized (race condition
   on scene load), the sound is silently skipped. No crash, no warning flood.
8. Volume for each category is configurable independently. Master volume is applied
   via Unity's Audio Mixer (one mixer group, no submixes needed at this scope).

### Sound Categories and Event Mapping

| Category | AudioSource Name | Play Mode | Trigger Event | Trigger Condition |
|----------|-----------------|-----------|---------------|-------------------|
| `word_found` | `SFX_WordFound` | `Play()` | `BoardManager.OnSubmissionAttempted` | `result.IsValid == true` |
| `invalid_word` | `SFX_InvalidWord` | `Play()` | `BoardManager.OnSubmissionAttempted` | `result.IsValid == false` |
| `reel_advance` | `SFX_ReelAdvance` | `PlayOneShot()` | `ReelController.OnReelAdvanced` | Always (one event per reel) |
| `low_turns` | `SFX_LowTurns` | `Play()` | `TurnManager.OnTurnsChanged` | `turnsRemaining <= LowTurnsThreshold` |
| `tick` | `SFX_Tick` | `Play()` loop | `TimerSystem.OnLowTime` | Always (fires once; looping starts) |
| `game_over` | `SFX_GameOver` | `Play()` | `GameModeManager.OnGameOver` | Always |

### AudioRandomContainer Assets

Each category has one `AudioRandomContainer` asset containing 2-4 audio clip
variants. Container assets live at `assets/audio/sfx/`:

| Container Asset | Variants | Variation Type |
|----------------|----------|----------------|
| `SFX_WordFound.asset` | 3 | Pitch variation (±5 semitones), randomized |
| `SFX_InvalidWord.asset` | 2 | Alternate tonal color (dull vs. buzzy) |
| `SFX_ReelAdvance.asset` | 4 | Mechanical click variants (subtle timbre differences) |
| `SFX_LowTurns.asset` | 1 | No variation needed — plays once per session |
| `SFX_Tick.asset` | 2 | Slight pitch difference to prevent exact loop repetition |
| `SFX_GameOver.asset` | 2 | Minor vs. major fanfare variant (for high score vs. normal end) |

Note: `SFX_GameOver` uses two variants to distinguish a high-score game over (major
fanfare) from a normal game over (minor/neutral fanfare). The Audio Manager must
receive the `IsNewHighScore` flag from `GameModeManager.OnGameOver` to select the
appropriate variant. Implementation: assign the major-fanfare clip to the container's
first slot and use a direct `AudioSource.clip` override rather than the container
when `IsNewHighScore == true`, or use a second `AudioRandomContainer` for each case.
The simpler two-container approach is recommended. See Edge Cases.

### Reel Advance Pitch Variation

The `reel_advance` sound applies a per-reel pitch offset in addition to container
variation, to give each reel a slightly distinct click character and make multi-reel
advances sound like a cascade rather than a mono event:

```
PitchOffset[reelIndex] = BasePitch + (reelIndex * PitchStepPerReel)
```

Variables:
- `BasePitch` (float): 1.0 (no shift). Range: [0.9, 1.1].
- `PitchStepPerReel` (float): 0.02. Range: [0.0, 0.05].
- `reelIndex` (int): 0-based index of the reel that advanced. Range: [0, 5].

Example: reel 0 plays at pitch 1.0, reel 5 plays at pitch 1.10. The difference is
subtle (a major second across all 6 reels) but enough to convey that multiple
distinct physical objects are moving.

### Tick Loop Management

The `tick` loop requires explicit lifecycle management:

```
on TimerSystem.OnLowTime:
    SFX_Tick.loop = true
    SFX_Tick.Play()

on TimerSystem.OnTimerExpired:
    SFX_Tick.Stop()
    SFX_GameOver.Play()   // or Play() with IsNewHighScore check

on GameStateMachine.OnExitPlaying (Paused or any other exit):
    if SFX_Tick.isPlaying:
        SFX_Tick.Pause()   // not Stop() — preserves loop position

on GameStateMachine.OnEnterPlaying (resuming from Pause):
    if SFX_Tick.clip != null and TimerSystem.TimeRemaining <= LowTimeThreshold:
        SFX_Tick.UnPause()
```

### States and Transitions

The Audio Manager has no formal state machine. Its internal state consists only of:
- Whether the `tick` loop is currently playing (read from `SFX_Tick.isPlaying`).
- Whether it has completed initialization (`_initialized` bool).

### Interactions with Other Systems

**Board Manager**: Audio Manager subscribes to `OnSubmissionAttempted`. Receives a
`SubmissionResult` struct containing at minimum `IsValid` (bool).

**Reel Controller**: Audio Manager subscribes to `OnReelAdvanced(int reelIndex)` on
each of the 6 `ReelController` instances. Subscription occurs in `OnEnable()` or via
a service locator after the Board Manager initializes its reels.

**Turn Manager**: Audio Manager subscribes to `OnTurnsChanged(int turnsRemaining)`.
Plays `low_turns` when `turnsRemaining <= LowTurnsThreshold`.

**Timer System**: Audio Manager subscribes to `OnLowTime(float remaining)` and
`OnTimerExpired`. Manages the tick loop accordingly.

**Game Mode Manager**: Audio Manager subscribes to `OnGameOver(bool isNewHighScore)`.
Plays the appropriate game-over fanfare variant.

**Game State Machine**: Audio Manager subscribes to state transitions for tick loop
pause/resume management. Does not depend on Game State Machine for any other behavior.

---

## Formulas

### Reel Advance Pitch

```
pitch = BasePitch + (reelIndex * PitchStepPerReel)
SFX_ReelAdvance.pitch = pitch
SFX_ReelAdvance.PlayOneShot(SFX_ReelAdvance.clip)
```

Note: `PlayOneShot` does not respect `AudioSource.pitch`. To apply pitch variation
with `PlayOneShot`, use `AudioSource.PlayOneShot(clip, volumeScale)` and manage
pitch via a dedicated per-reel `AudioSource` pool, or switch to `Play()` with
`AudioSource.pitch` set before each call. The recommended approach is a small
pool of 6 `AudioSource` components (one per reel position) on the Audio Manager
`GameObject`, each pre-configured with its reel's `BasePitch + offset`. This avoids
`PlayOneShot`'s pitch limitation while still allowing overlapping playback.

**Revised reel advance implementation:**

```
// 6 AudioSource components: ReelAdvanceSources[0..5]
// Each pre-configured: ReelAdvanceSources[i].pitch = BasePitch + (i * PitchStepPerReel)

on ReelController[i].OnReelAdvanced:
    ReelAdvanceSources[i].Play()
```

`Play()` on a source that is already playing restarts it — acceptable for reel
advance since a reel cannot advance twice simultaneously.

### Volume

No formula — volume is a direct `[0.0, 1.0]` float knob per category. All category
sources route through a single Unity Audio Mixer group for master volume control.

---

## Edge Cases

**Event fires before Audio Manager initializes**: `_initialized` guard returns early.
Sound is skipped silently. This is acceptable; missing one SFX on the very first frame
is imperceptible.

**Scene reload re-creates event sources**: The Audio Manager uses `DontDestroyOnLoad`
and re-subscribes in `OnEnable()` or via a scene-loaded callback. Stale subscriptions
from the previous scene are unsubscribed in `OnDisable()` or `OnDestroy()` of the
event sources, whichever is more appropriate. The Audio Manager should defensively
unsubscribe from all known sources before re-subscribing to avoid duplicate event
handlers.

**Both `word_found` and `reel_advance` fire on the same submission**: Expected
behavior. `word_found` plays on `SFX_WordFound`, and each advanced reel fires
`OnReelAdvanced` on its `ReelController`, playing on the corresponding
`ReelAdvanceSources[i]`. These overlap intentionally — the click cascade of reel
advances underlies the word-found sound.

**`invalid_word` fires while `word_found` is still playing** (fast submissions):
`SFX_InvalidWord.Play()` restarts that source. `SFX_WordFound` continues playing
uninterrupted on its own source. No overlap issue since they are separate sources.

**Game over fires while tick is looping**: `SFX_Tick.Stop()` is called in the
`OnTimerExpired` handler before `SFX_GameOver.Play()`. The order is deterministic
since both are handled in the same event callback.

**Game over fires in turn-limit mode** (no tick loop active): `SFX_Tick.isPlaying`
is false; `SFX_Tick.Stop()` is a no-op on a stopped source. Safe.

**`OnLowTime` fires but timer mode is not active** (defensive): Timer System only
fires `OnLowTime` when running, which only occurs in timer mode. However, if a bug
causes this in turn-limit mode, the tick loop would start playing incorrectly.
Mitigate by having Audio Manager check `TimerSystem.State == Running` before
starting the tick loop.

**`low_turns` fires multiple times** (e.g., `OnTurnsChanged` fires on every turn):
The `low_turns` sound should play only when crossing below the threshold, not on
every subsequent turn. Implement a `_lowTurnsSoundPlayed` bool flag that is set on
first trigger and reset when a new session starts.

**`IsNewHighScore` flag and game-over fanfare variant**: The Audio Manager uses two
separate `AudioSource` + `AudioRandomContainer` pairs: `SFX_GameOverNormal` and
`SFX_GameOverHighScore`. On `OnGameOver(isNewHighScore)`:
- If `isNewHighScore == true`: play `SFX_GameOverHighScore`.
- If `isNewHighScore == false`: play `SFX_GameOverNormal`.
This is cleaner than trying to override clips within a single container at runtime.

**Audio Manager destroyed before game over** (application quit): Unity's execution
order guarantees `OnApplicationQuit` before `OnDestroy` — no audio issue. The
`DontDestroyOnLoad` object is not destroyed during scene loads, so mid-session
scene transitions are safe.

---

## Dependencies

### This system requires from others

| System | What it needs |
|--------|--------------|
| Board Manager | `OnSubmissionAttempted(SubmissionResult result)` event, `result.IsValid` bool |
| Reel Controller | `OnReelAdvanced(int reelIndex)` event on each of the 6 instances |
| Turn Manager | `OnTurnsChanged(int turnsRemaining)` event |
| Timer System | `OnLowTime(float remaining)` event; `OnTimerExpired` event; `State` property (for defensive tick check) |
| Game Mode Manager | `OnGameOver(bool isNewHighScore)` event |
| Game State Machine | State transition events for tick loop pause/resume (`OnEnterPlaying`, `OnExitPlaying`) |
| Unity AudioRandomContainer | Unity 6 package; `AudioSource.resource` API |

### This system provides to others

The Audio Manager provides nothing to other systems. It is a pure consumer.

---

## Tuning Knobs

All values live in `assets/data/audio-config.json` or a ScriptableObject at
`assets/data/AudioConfig.asset`.

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `MasterVolume` | Feel | 1.0 | 0.0 – 1.0 | Overall SFX volume. Applied via Audio Mixer group. |
| `WordFoundVolume` | Feel | 0.9 | 0.0 – 1.0 | Volume of valid word sound. Slightly lower than max to leave headroom for game-over fanfare. |
| `InvalidWordVolume` | Feel | 0.6 | 0.0 – 1.0 | Volume of invalid word sound. Lower than word_found to make valid submissions feel more rewarding by contrast. |
| `ReelAdvanceVolume` | Feel | 0.5 | 0.0 – 1.0 | Volume of reel click. Should be subtle — it fires frequently. |
| `TickVolume` | Feel | 0.7 | 0.0 – 1.0 | Volume of the low-timer tick loop. Noticeable but not alarming at default. |
| `GameOverVolume` | Feel | 1.0 | 0.0 – 1.0 | Volume of game-over fanfare. Full volume — this is the session climax. |
| `BasePitch` | Feel | 1.0 | 0.9 – 1.1 | Base pitch for reel advance sounds. |
| `PitchStepPerReel` | Feel | 0.02 | 0.0 – 0.05 | Pitch increment per reel index for the advance sound cascade. |
| `LowTurnsThreshold` | Gate | 3 | 1 – 5 | Number of turns remaining that triggers the `low_turns` warning sound. Must match the HUD's visual warning threshold for consistency. |

**Note on `LowTurnsThreshold`**: This value must be kept in sync with the Turn Manager's
own low-turns HUD threshold. If the HUD pulses red at 3 turns and the audio fires at 5,
the mixed signals create confusion. Consider making this a shared constant or a value
both systems read from the same config asset.

---

## Acceptance Criteria

### Functional

- [ ] Valid word submission plays a `word_found` sound variant within one frame of the event.
- [ ] Invalid word submission plays an `invalid_word` sound within one frame of the event.
- [ ] Each reel advance plays a distinct reel click (reel 0 and reel 5 have audibly different pitches).
- [ ] A 4-letter word submission causes 4 overlapping reel click sounds (not one merged sound).
- [ ] `OnTurnsChanged` with `turnsRemaining == 3` plays the `low_turns` sound exactly once per session.
- [ ] `OnTurnsChanged` with `turnsRemaining == 2` does not play `low_turns` again.
- [ ] `OnLowTime` starts the tick loop; it continues looping until `OnTimerExpired` fires.
- [ ] `OnTimerExpired` stops the tick loop and plays the game-over fanfare on the same frame.
- [ ] Pausing the game pauses the tick loop (it does not continue during the pause menu).
- [ ] Resuming from pause resumes the tick loop (if timer is still in low-time).
- [ ] A new high score game over plays the high-score fanfare variant.
- [ ] A normal game over plays the standard fanfare variant.
- [ ] Scene reload does not result in duplicate event subscriptions (test: submit a word after scene reload — only one `word_found` sound plays, not two).
- [ ] All sounds play at their configured volumes (verify with Unity Audio Mixer inspector).

### Experiential (Playtest Validation)

- [ ] Playtesters describe the reel advance sounds as "satisfying" or "clicky" (not "annoying" or "repetitive") after a 5-minute session.
- [ ] Playtesters in timer mode report that the tick sound increases their sense of urgency.
- [ ] No playtester reports hearing two of the same sound when submitting a single word.
- [ ] The game-over fanfare feels like an appropriate session-end signal (neither too abrupt nor too long).

# Scene Flow

> Status: Designed | Author: Claude Code Game Studios | Last Updated: 2026-03-26

---

## Overview

Scene Flow is a leaf-node infrastructure system that responds to Game State Machine transitions by asynchronously loading and unloading Unity scenes. It subscribes exclusively to `GameStateMachine.OnStateChanged` and maps each relevant transition to a scene operation: loading `"MainMenu"` or `"Game"` via `SceneManager.LoadSceneAsync`. All transitions are accompanied by a simple screen fade (black overlay, configurable duration) to mask the loading process. Scene Flow has no upstream dependents — it is the final consumer in the state propagation chain and produces no events of its own. It does not own any gameplay state; its only job is keeping the visible scene synchronized with the application state.

---

## Player Fantasy

Like all infrastructure systems, Scene Flow is invisible when working correctly. It contributes to the player's sense of polish and professionalism: transitions between the main menu and the game board feel intentional, with a brief fade that signals "something is changing" rather than a jarring instant cut. The loading process should be imperceptible for a project of this scope — the fade exists primarily as visual punctuation, not to hide a genuine load time.

MDA Aesthetics served: **Sensation** (the subtle feel quality of smooth scene transitions).

---

## Detailed Design

### Core Rules

1. Scene Flow maintains a reference to the Game State Machine and subscribes to `OnStateChanged(GameState previous, GameState next)` during its own `Awake()`.

2. **Scene name constants** (defined in code, not in external config, as they map directly to Unity build settings):
   - `"MainMenu"` — the main menu scene
   - `"Game"` — the gameplay scene (board, HUD, and game-over overlay are all part of this scene)
   - There is no separate `"GameOver"` scene. The Game Over Screen is a Canvas overlay within the `"Game"` scene, toggled by its own listener on `OnStateChanged`.

3. **Transition-to-operation mapping:**

   | Transition | Operation |
   |-----------|-----------|
   | `MainMenu → Playing` | Fade out → Load `"Game"` async → Fade in |
   | `GameOver → MainMenu` | Fade out → Load `"MainMenu"` async → Fade in |
   | `Playing → Paused` | No scene operation (Pause Menu is an overlay in `"Game"`) |
   | `Paused → Playing` | No scene operation |
   | `Playing → GameOver` | No scene operation (Game Over Screen is an overlay in `"Game"`) |

4. **Async load procedure** (for transitions that require a scene load):
   - Begin fade-out coroutine (black overlay alpha 0 → 1, duration = `FadeOutDuration`).
   - Wait for fade-out to complete.
   - Call `SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single)`.
   - Poll `AsyncOperation.progress` until `isDone == true`.
   - Begin fade-in coroutine (black overlay alpha 1 → 0, duration = `FadeInDuration`).
   - Scene is now fully presented.

5. **Input lock during transition:** Scene Flow sets a `_isTransitioning` flag to `true` at the start of a scene-loading operation and `false` at the end. While `_isTransitioning` is `true`, Scene Flow ignores any further `OnStateChanged` events. (In practice this cannot occur because the Game State Machine's transition guard prevents state changes while a transition is processing, but Scene Flow guards independently for safety.)

6. **Fade overlay implementation:** A full-screen black `Image` component on a Canvas with `Sort Order = 999` (rendered above all other UI). Scene Flow controls its `CanvasGroup.alpha` via a coroutine. This Canvas persists across scene loads and is placed in a `DontDestroyOnLoad` object owned by Scene Flow.

7. **`LoadSceneMode.Single`** is used for all loads. This unloads the current scene and loads the target scene. No additive scene loading is used in the MVP architecture.

8. Scene Flow does not handle loading failures at the MVP level. If `LoadSceneAsync` fails (invalid scene name, missing from build settings), Unity will throw an exception that surfaces in the console. A post-MVP enhancement should add explicit error handling and a fallback to `MainMenu`.

### States and Transitions

Scene Flow has a simple internal state:

| State | Meaning |
|-------|---------|
| `Idle` | No scene operation in progress; listening for state changes |
| `Transitioning` | A fade+load sequence is running; ignoring new state change events |

```
Idle ──[scene-loading transition detected]──► Transitioning
Transitioning ──[fade-in complete]──────────► Idle
```

Transitions that do not require a scene load (Pause, Resume, GameOver overlay) are processed in `Idle` state and take zero time (no state change for Scene Flow itself).

### Interactions with Other Systems

**Game State Machine (listens to):** Scene Flow subscribes only to `OnStateChanged`. It reads the `previous` and `next` state values to determine which operation to perform.

**Unity SceneManager (calls):** Scene Flow calls `SceneManager.LoadSceneAsync(string, LoadSceneMode)` from Unity's `UnityEngine.SceneManagement` namespace. It stores and polls the returned `AsyncOperation`.

**No downstream dependents:** Scene Flow fires no events. No system subscribes to Scene Flow.

---

## Formulas

### Fade Duration

```
FadeTotalDuration = FadeOutDuration + FadeInDuration
  default: 0.25s + 0.25s = 0.50s total
  range: FadeOutDuration ∈ [0.0, 2.0], FadeInDuration ∈ [0.0, 2.0]
```

### Fade Alpha Curve

Both fade-out and fade-in use a linear interpolation over time:

```
FadeOut:  alpha(t) = t / FadeOutDuration,  t ∈ [0, FadeOutDuration]
FadeIn:   alpha(t) = 1 - (t / FadeInDuration),  t ∈ [0, FadeInDuration]

  where alpha ∈ [0.0, 1.0], 0 = transparent, 1 = fully black
```

A non-linear (ease-in / ease-out) curve may be substituted at the discretion of the UI artist during polish without changing the design contract. The duration parameters remain the authoritative tuning knob.

### Minimum Perceived Load Time

```
PerceivedLoadTime = FadeOutDuration + ActualLoadTime + FadeInDuration
```

For this project, `ActualLoadTime` is expected to be < 0.1 seconds (small scene, minimal assets). The fade therefore dominates the perceived transition duration. If load times increase in future builds, `FadeOutDuration` provides natural cover.

---

## Edge Cases

**Scene not in build settings:** `SceneManager.LoadSceneAsync` will log a Unity error and fail silently (or throw, depending on Unity version). Scene Flow should log a descriptive error: `[SceneFlow] Failed to load scene '{sceneName}' — verify it is included in File > Build Settings.` At MVP, the game is left in an undefined visual state. Post-MVP: add a fallback that forces a load of `"MainMenu"`.

**`OnStateChanged` fires while a fade is in progress:** Scene Flow's `_isTransitioning` guard discards the event. In valid operation this cannot happen (the Game State Machine's own guard prevents stacked transitions), but defensive guarding is warranted.

**`MainMenu → Playing` transition called before the `"Game"` scene is added to build settings:** Same as above. Developer error caught at build time in most configurations.

**`FadeOutDuration` or `FadeInDuration` set to 0:** Legal. Alpha snaps to final value in a single frame. Produces an instant cut. Acceptable for development/testing builds.

**`FadeOutDuration` set to a very large value (e.g. 10 seconds):** Legal but creates a frustrating player experience. The safe range cap in tuning knobs (max 2.0 seconds) should prevent this in production.

**Application quit during a scene transition:** Unity's application quit process tears down all objects. No special handling needed — the coroutine will be interrupted cleanly. No state needs to be saved during a scene transition in this game.

**Device with very slow scene loading (e.g. old mobile hardware, post-MVP):** The `AsyncOperation.progress` poll correctly waits for `isDone` regardless of how long loading takes. The fade-out completes before loading begins, so a fully black screen is shown during any extended load. No timeout is required at MVP (PC target, fast loads expected).

**Double `GameOver → MainMenu` transition trigger (e.g. player spams the button):** The Game State Machine rejects the second transition (`GameOver → MainMenu` from `MainMenu` is invalid). Scene Flow never sees it.

---

## Dependencies

### Provided to Other Systems

Scene Flow fires no events and provides no public API to other systems. It is a pure consumer.

### Required from Other Systems (Inbound)

| System | Dependency Type | Usage |
|--------|----------------|-------|
| Game State Machine | Event subscription: `OnStateChanged(GameState prev, GameState next)` | Determines when and what to load |

### Unity Engine Dependencies

| API | Usage |
|-----|-------|
| `UnityEngine.SceneManagement.SceneManager.LoadSceneAsync` | Async scene loading |
| `UnityEngine.SceneManagement.LoadSceneMode.Single` | Unload current, load target |
| `UnityEngine.AsyncOperation` | Progress polling and completion detection |
| `UnityEngine.UI.Image` / `UnityEngine.CanvasGroup` | Fade overlay rendering |
| `UnityEngine.Object.DontDestroyOnLoad` | Persist fade overlay across scene loads |
| `UnityEngine.Coroutine` | Fade animation sequencing |

### Lifecycle Dependency

Scene Flow must initialize after Game State Machine (to subscribe to `OnStateChanged`). Scene Flow's `DontDestroyOnLoad` object must persist for the entire application lifetime. It should be initialized in the first scene loaded by Unity (which is `"MainMenu"`).

---

## Tuning Knobs

| Knob | Category | Default | Safe Range | Effect |
|------|----------|---------|------------|--------|
| `FadeOutDuration` | Feel | 0.25 | 0.0–2.0 | Duration in seconds for the screen to fade to black. Controls transition pacing. |
| `FadeInDuration` | Feel | 0.25 | 0.0–2.0 | Duration in seconds for the screen to fade in from black. Controls transition pacing. |
| `FadeOverlayColor` | Feel | `(0, 0, 0, 1)` (black) | Any RGBA | Color of the full-screen transition overlay. Black is standard; other colors are possible for stylistic effect. |
| `FadeSortOrder` | Feel | 999 | 100–9999 | Canvas sort order of the fade overlay. Must be higher than all other UI canvases to render on top. |

All tuning knobs are defined in `assets/data/scene-flow-config.json`.

---

## Acceptance Criteria

### Functional Criteria

- **FC-SF-01**: When `OnStateChanged(MainMenu, Playing)` is received, the `"Game"` scene loads and becomes the active scene. The `"MainMenu"` scene is unloaded.
- **FC-SF-02**: When `OnStateChanged(GameOver, MainMenu)` is received, the `"MainMenu"` scene loads and becomes the active scene. The `"Game"` scene is unloaded.
- **FC-SF-03**: When `OnStateChanged(Playing, Paused)` is received, no scene operation occurs.
- **FC-SF-04**: When `OnStateChanged(Paused, Playing)` is received, no scene operation occurs.
- **FC-SF-05**: When `OnStateChanged(Playing, GameOver)` is received, no scene operation occurs.
- **FC-SF-06**: A full-screen black overlay fades from alpha 0 to alpha 1 before any scene load begins.
- **FC-SF-07**: After the scene load completes, the overlay fades from alpha 1 to alpha 0.
- **FC-SF-08**: No `OnStateChanged` event received during a fade-and-load sequence triggers an additional scene load.
- **FC-SF-09**: The fade overlay renders above all other UI elements during a transition.
- **FC-SF-10**: The fade overlay `CanvasGroup.alpha` returns to exactly 0.0 after fade-in completes (no residual dimming).

### Experiential Criteria (Playtest Validation)

- **EC-SF-01**: Players describe scene transitions as "smooth" or do not notice them at all. (Validated by post-session interview question: "Did the transitions between menu and game feel jarring or smooth?")
- **EC-SF-02**: No observable frame of incorrect content is visible during a transition (i.e. no flash of the old scene or an uninitialized new scene). (Validated by recording transitions at 60fps and reviewing frame-by-frame.)

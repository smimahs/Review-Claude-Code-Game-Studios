# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6000.3.8f1 (Unity 6.3 LTS)
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP) — strategic direction for Unity 6; HDRP is maintenance-only
- **Physics**: Unity 2D Physics (Box2D built-in) — standard Rigidbody2D/Collider2D API

## Naming Conventions

- **Classes**: PascalCase (e.g., `ReelController`)
- **Variables (public)**: PascalCase (e.g., `MoveSpeed`)
- **Variables (private)**: _camelCase (e.g., `_reelIndex`)
- **Methods**: PascalCase (e.g., `SubmitWord()`)
- **Events/Actions**: PascalCase with "On" prefix (e.g., `OnWordSubmitted`)
- **Files**: PascalCase matching class (e.g., `ReelController.cs`)
- **Scenes/Prefabs**: PascalCase matching root (e.g., `GameBoard.unity`, `ReelSlot.prefab`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `MAX_REELS`)

## Performance Budgets

- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: [TO BE CONFIGURED — <100 typical for 2D puzzle game]
- **Memory Ceiling**: [TO BE CONFIGURED]

## Testing

- **Framework**: Unity Test Framework 1.4.x (NUnit 3.5)
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)
- **Pattern**: Keep Trie, scoring, and reel state in plain C# classes (no MonoBehaviour) for fast `[Test]` unit tests. Use `[UnityTest]` only for scene-level integration tests.

## Forbidden Patterns

- `Object.FindObjectsOfType<T>()` — compile error in Unity 6; use `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`
- `Object.FindObjectOfType<T>()` — use `Object.FindFirstObjectByType<T>()` instead
- Legacy Input Manager (`UnityEngine.Input`) — use new Input System only
- UGUI for new UI — use UI Toolkit (UGUI is in maintenance mode in Unity 6)
- Built-in Render Pipeline (BIRP) — deprecated in Unity 6.5, do not use for new projects
- `[SerializeField]` directly on properties — compile error in Unity 6.3+; use `[field: SerializeField]` on auto-properties

## Allowed Libraries / Addons

- `com.unity.inputsystem` — new Input System (default in Unity 6)
- UI Toolkit — built-in, no separate package needed
- `com.unity.test-framework` — Unity Test Framework 1.4.x

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

# ReelWords — Game Concept

**Version**: 0.1
**Created**: 2026-03-26
**Status**: Draft

---

## Overview

ReelWords is a 2D word puzzle game for PC built in Unity 6. Six reels are displayed
like a slot machine; each reel holds a fixed sequence of characters that never
randomizes. The player reads valid words left-to-right across the reels and submits
them. Only the reels whose characters were used in the submitted word advance to
the next character in their sequence — all other reels stay fixed. This creates a
strategic layer: every word choice shapes the future state of the board.

---

## Player Fantasy

The player feels like a code-breaker solving a living puzzle. Each word they find
changes the board in a controlled, predictable way, rewarding players who think
ahead. The satisfying "click" of letters locking into high-value combinations —
and the panic of a ticking clock or dwindling turns — drives repeated play.

---

## Core Mechanic

- **6 reels**, each containing a fixed, non-randomized sequence of characters
- Characters on each reel cycle through their sequence deterministically
- The player selects a contiguous set of reels (left to right) to form a word
- On submission, the game validates the word against a dictionary (Trie-based)
- Valid word: reels whose characters were used **advance one step** in their sequence
- Invalid or already-used word: no advancement; turn/time is consumed
- Scoring: each letter has a Scrabble-style point value; word score = sum of letter values

---

## Game Modes

### Turn Limit Mode (Primary)
- Player has a fixed number of turns per game
- Each word submission (valid or invalid) consumes one turn
- Game ends when turns reach zero; final score is displayed

### Timer Mode (Secondary)
- Player has a time limit per game
- Valid submissions freeze the timer briefly (bonus time mechanic TBD)
- Game ends when timer reaches zero

---

## Technical Foundation

- **Engine**: Unity 6000.3.8f1 (Unity 6.3 LTS)
- **Language**: C#
- **Render Pipeline**: URP (2D Renderer)
- **Dictionary**: Trie data structure for O(k) word lookup (k = word length)
- **Reel Sequences**: Data-driven — reel character sequences defined in external config

---

## Scope & Deliverables

This is a complete small pet project. Target deliverables:

- Full playable game loop (turn-limit mode + timer mode)
- English word dictionary (curated common words, Scrabble-compatible letter values)
- Complete UI: main menu, game board, score display, game-over screen
- Visual polish: slot machine aesthetic, reel spin animations, word highlight feedback
- All source code, assets, and configuration

---

## MVP Definition

Required for a playable first build:

- [ ] 6 reels with fixed character sequences, cycling on word submit
- [ ] Trie-based dictionary with at least 5,000 common English words
- [ ] Word submission and validation
- [ ] Turn-limit game mode
- [ ] Score tracking (letter values → word score → session total)
- [ ] Basic UI: board display, score label, turns remaining, submit/clear controls
- [ ] Game over screen with final score

---

## Post-MVP / Full Vision

- Timer mode
- Reel sequence balancing (letter frequency tuning for playability)
- Visual polish and animations (reel spin, word pop, score fly-up)
- Sound design (click SFX, word-found fanfare, tick for timer)
- High score / leaderboard (local)
- Multiple dictionary sizes or word categories (easy / hard mode)
- Accessibility: colorblind palette, text scaling

---

## Design Pillars

1. **Strategic clarity** — The board state is always legible; the player knows exactly what will happen when they submit a word
2. **Satisfying progression** — Every word found visibly changes the board, giving constant feedback of progress
3. **Replayability through determinism** — Fixed reel sequences mean skilled players can learn and optimize, not just get lucky

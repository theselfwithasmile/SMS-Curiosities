# [Project Name — see naming notes]

A modular Unity puzzle framework, built to demonstrate that a wide range of commercially-recognizable mobile puzzle genres can be expressed as different configurations of the same small set of systems, rather than implemented one by one as separate minigames.

![Unity](https://img.shields.io/badge/Unity-black?logo=unity)
![Status](https://img.shields.io/badge/status-in%20development-yellow)
![Platform](https://img.shields.io/badge/platform-mobile%20%2F%202D-blue)

<p align="center">
  <!-- TODO: swap in an actual gameplay gif/screenshot, e.g.: -->
  <!-- <img src="demo.gif" width="850" alt="Gameplay"> -->
</p>

---

## Overview

Six commercially-recognizable puzzle genres — Water Sort, Block Puzzle, Merge, Toon Blast, Tile Connect, and Parking Jam — currently run on one shared architecture instead of six separate codebases. Each is expressed as a different composition of the same handful of systems (layout, constraints, completion predicates, resolutions), with its own procedural generator and its own way of guaranteeing the result is actually solvable.

The project demonstrates reusable architecture, procedural generation with correctness guarantees (not just "looks plausible"), and enough commercial awareness of the genre to know which corners can be cut and which can't.

---

## Core Philosophy

Rather than implementing puzzle genres individually, identify the **common grammar** behind them and express each genre as a different composition of shared systems.

### Token

The fundamental manipulable entity — one component (`Token` in code) handling dragging, placement, and per-genre behavior data.

Implemented as: a water layer (Water Sort), a car (Parking Jam), a tile (Tile Connect / Toon Blast), a stackable product (Merge). The same abstraction generalizes to genres not yet built here — a screw, a rope endpoint — without changing `Token` itself.

### Puzzle Group

A logical evaluation unit (`Container` in code). A group:

* owns tokens
* defines entry/exit constraints
* evaluates a completion predicate
* executes a resolution when complete
* optionally reacts to drops on an already-occupied slot (an occupant interaction — Merge's combine, Toon Blast's swap)

### Interaction

How the player manipulates tokens. Currently: **drag**, unified across mouse and touch through Unity's `EventSystem` so the same code path drives both.

### Constraint

Determines whether a placement is legal. Implemented forms: capacity, same-group entry, no-entry (seed-once cells), reachability (routed connection), escape-lane clearance.

### Completion Predicate

When a puzzle group is considered solved. Implemented forms: full, empty, tier reached, all escaped.

### Resolution

What happens after completion. Implemented forms: clear and destroy, spawn a grouped token elsewhere, merge into a higher tier, reveal whatever's buried underneath (layering).

---

## Puzzle Genres (Implemented)

| Genre | Container / Layout | Constraint | Completion | Generation approach |
|---|---|---|---|---|
| Water Sort | Region-grown tubes | Capacity + same-group entry | Tube full (homogeneous) | Quota-matched supply |
| Block Puzzle (Woodoku-style) | Overlapping row + column containers | Capacity | Row/column full | Canonical shape batches, refilled on placement |
| Merge (2048-style) | Per-tile cell | Occupant interaction (merge on match) | Target tier reached | Seeded so the target tier is always reachable |
| Toon Blast (match-3) | Per-tile cell, optional layering | Occupant interaction (swap-to-match) | Board cleared | Quota-matched groups |
| Tile Connect (Onet-style) | Per-tile cell, no direct entry | Reachability (routed connection) | Board cleared | Reverse construction (build backward from a solved state) |
| Parking Jam (arrow-maze) | None — pure grid occupancy | Escape-lane clearance | Every piece escaped | Greedy monotone solvability check |

No single "universal solver" backs all six — guaranteeing solvability means something structurally different for a homogeneous-tube puzzle than for an order-dependent connection puzzle, so each genre gets the generation technique that actually fits its own structure.

---

## Gameplay Features

- **Modular architecture**: a puzzle genre is a composition of shared pieces (layout strategy, constraints, completion predicate, resolution), not bespoke per-genre code.
- **Six puzzle genres, one pipeline**: every genre above runs through the same `BaseZone` generate → validate → commit flow.
- **Solvability-guaranteed procedural generation**: quota-matched supply, reverse construction, and greedy monotone verification, chosen per genre rather than forced through one generic solver.
- **Layering**: a cell can hold several stacked tokens; clearing the visible one reveals whatever's buried underneath.
- **Game flow & difficulty scaling**: a persistent flow manager drives Menu → Playing → Win/Lose → Retry/Next Level, with each zone's board size and spawn counts scaling off a single shared difficulty knob.
- **Procedural rendering**: grid dots, container regions, and directional indicators are all GPU-instanced procedural meshes — no board art assets required.
- **Animated, themed tokens**: tokens play hover/drag flipbook animations via Unity's Playables API, resolved per group and per merge tier.
- **Tweened feedback**: a small custom coroutine-based tween system (no external dependency) drives pickup pops, merge convergence, buried-token reveals, and clear cascades.

---

## Tech Stack

- **Engine:** Unity (2D, URP)
- **Language:** C#
- **Rendering:** GPU instancing (`Graphics.DrawMeshInstanced`) for all procedural board visuals; Unity Playables API (`AnimationClipPlayable`) for hand-driven, non-legacy sprite animation
- **Input:** uGUI `EventSystem`, unifying mouse and touch drag through one code path
- **Dependencies:** none for tweening or animation — both are small custom systems built for this project

---

## Project Structure

```
Assets/Scripts/
  Layout/
    Token.cs               # Draggable manipulable entity; per-genre placement resolution
    TokenSpawner.cs         # Spawning, animation resolution, footprint placement
    Container.cs            # Constraint/predicate/resolution bundle; layering (buried tokens)
    ContainerRules.cs        # Concrete constraints, predicates, resolutions, occupant interactions
    ContainerManager.cs      # Container registration, lookup, shared generation utilities
    ContainerLayout.cs       # Reusable layout strategies (per-tile, orthogonal, region-growth)
    Grid.cs                  # Pure geometry: coordinate conversion, occupancy, bench reservation
  Zone Variants/
    BaseZone.cs              # Shared generation pipeline, win/lose detection, difficulty scaling
    WaterSortBaseZone.cs
    BlockPuzzleBaseZone.cs
    MergeBaseZone.cs
    ToonBlastBaseZone.cs
    TileConnectBaseZone.cs
    ParkingJamBaseZone.cs
  Visuals/
    GridRenderer.cs          # GPU-instanced dot/container rendering
    TweenRunner.cs            # Coroutine-based tween utility
    MergeEffect.cs
    ConnectPathEffect.cs
  GameFlowManager.cs         # Menu/Playing/Won/Lost state, difficulty progression, scene reload
  GameFlowUI.cs              # Flow-state -> panel visibility wiring
  GameState.cs               # Global color/group palette
  Spawner.cs                 # Zone selection/cycling for the current build
```

---

## Controls

| Input | Action |
|---|---|
| Click/tap + drag | Move a token |
| Release over a valid slot | Attempt placement, match, or merge |
| R | Cycle to the next puzzle genre (development build) |

Mouse and touch share the same drag path via uGUI's `EventSystem` — no separate mobile input handling.

---

## Status

Core systems are implemented and running: the shared Token/Container/Zone pipeline, all six puzzle genres, a solvability-guaranteed generator per genre, layering, animated token art, procedural board rendering, and the full Menu → Playing → Win/Lose → Retry/Next Level flow with difficulty scaling.

The remaining work is mostly presentation and breadth, not architecture: the menu/win/lose screens are currently functional placeholders (no styling or transitions yet), there's no audio, and a couple of interaction ideas explored during development — collision-aware dragging for Tile Connect, and a queue-based clearing mechanic — are deliberately deferred rather than built.

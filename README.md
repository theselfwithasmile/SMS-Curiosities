## Project Goal

A **modular Unity puzzle framework** inspired by the dominant mobile puzzle market, rather than a collection of independent minigames. The project demonstrates scalability, reusable architecture, procedural generation, and commercial awareness, all under a cohesive artistic direction.

---

## Core Philosophy

Rather than implementing puzzle genres individually, identify the **common grammar** behind them and express each genre as a different composition of shared systems.

---

## Common Architecture

### Token

The fundamental manipulable entity.

Examples:

* Water layer
* Car
* Screw
* Rope endpoint
* Product
* Tile

---

### Puzzle Group

A logical evaluation unit (formerly "container").

A group:

* owns Tokens (or references them)
* defines constraints
* evaluates completion
* executes a completion action

---

### Interaction

How the player manipulates PuzzleObjects.

Examples:

* Drag
* Transfer
* Remove
* Slide
* Swap

---

### Constraint

The primary gameplay system.

Determines whether an interaction is legal.

Examples:

* Capacity
* Collision
* Occupancy
* Dependency
* Grouping
* Connectivity
* Crossing

---

### Completion Predicate

When a Puzzle Group is considered solved.

Examples:

* Same color
* Empty
* Full
* No crossings
* Escaped

---

### Resolution

What happens after completion.

Examples:

* Destroy objects
* Destroy group
* Merge objects
* Spawn new object
* Unlock another group

---

## Puzzle Archetypes

| Game         | Group     | Constraint          | Completion   |
| ------------ | --------- | ------------------- | ------------ |
| Water Sort   | Tube      | Capacity + Color    | Homogeneous  |
| Goods Sort   | Shelf     | Capacity + Grouping | Full         |
| Screw Puzzle | Plank     | Dependency          | Empty        |
| Parking Jam  | World     | Collision           | All escaped  |
| Merge / 2048 | Grid Cell | Occupancy           | Merge        |
| Match-3      | Grid      | Adjacency           | ≥3 connected |
| Rope Puzzle  | Rope      | Crossing topology   | No crossings |

---

## Procedural Generation

Each level is generated from:

* interaction type
* group layout
* object count
* constraint composition
* emoji theme

Difficulty scales primarily through **constraint complexity**, with object count as an additional parameter.
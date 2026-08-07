# Batch 5 Architecture and Ownership

## Dependency boundary

```text
WavePrototype.Editor
    ├── WavePrototype.Presentation
    └── WavePrototype.Simulation

WavePrototype.Presentation
    └── WavePrototype.Simulation

WavePrototype.Simulation
    └── UnityEngine math only; no presentation or editor dependency
```

The three folders contain assembly definitions, so the dependency direction is enforced by the compiler rather than folder convention.

## Authoritative ownership

`WaveSimulation` privately owns:

- Waves
- Boats
- Public events
- Pending events
- Wave and boat decision buffers
- Tick-addressed input state
- Source runtime state
- System ordering and Apply

Presentation receives `IReadOnlyList<T>` views. Entity changes occur through explicit coordinator methods such as `AddBoat`, `SpawnWave`, and `QueueBoatControl`.

## System responsibilities

### `WaveSourceSystem`

- Defines three boundary sources from world extents.
- Owns the source random stream and next wave ID.
- Populates the initial world along source travel paths.
- Replenishes expired packets at source boundaries.
- Creates manual packets with source ID zero.

### `WavePropagationSystem`

- Samples depth and depth gradients.
- Computes speed, shoaling, deep-water recovery, and refraction.
- Decides breaking, energy decay, rock absorption, land response, and expiry.
- Writes `WaveDecision` values without mutating authoritative waves.

### `WaveBoatInteractionSystem`

- Evaluates elongated wave footprints against boats.
- Accumulates temporary force, yaw, slowdown, surfing, and breaking damage.
- Does not mutate waves or boats.

### `BoatMotionSystem`

- Consumes held tick controls and accumulated interaction decisions.
- Applies propulsion, steering, drag, cruise/surf limits, and overspeed decay.
- Resolves grounding and deterministic swept rock contact with tangential escape.
- Writes `BoatDecision` values without mutating authoritative boats.

### `BoatInputBuffer`

- Accepts controls addressed by simulation tick and boat ID.
- Replaces duplicate commands for the same tick and boat.
- Holds the last applied control until another command arrives.
- Retains the applied stream for deterministic replay.

### `WaveSimulation`

- Creates the environment through `IOceanEnvironmentFactory`.
- Runs Observe/Decide systems in a fixed order.
- Applies all persistent entity changes.
- Replenishes the sea only during Apply.
- Publishes pending events only after Apply.
- Produces the complete authoritative state hash and density queries.

## Current limits

- Simulation math still uses `UnityEngine.Vector2` and `Mathf`; independence currently means independence from Unity lifecycle, physics, presentation, and editor APIs—not compilation without Unity libraries.
- `SimulationConfig` is centralized but remains mutable for prototype tuning.
- Recorded commands are held in memory; there is no save-file format or player-facing replay UI.
- The visible-density target is measured but not enforced.
- Source definitions are created in code rather than authored as assets.
- Wide wave fronts still sample land, depth, and rocks primarily at their centers. Cross-crest environment sampling remains ocean-coherence work for a later batch.

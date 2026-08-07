# Batch 6 Environment and Target Design

## Scope boundary

Batch 6 adds space and lightweight exploration structure without deepening the sailing
control scheme. The world grows from 180×100 to 360×200, while the normal wave count
stays at 80. Uneven wave distribution remains valid.

## Analytic shelf model

`OceanEnvironment.SampleDepth` composes a small number of broad functions:

1. A quiet deep-ocean baseline.
2. An irregular eastern coastline with nearshore, continental-shelf, continental-slope,
   and deep-water bands.
3. Overlapping rotated elliptical profiles for island land, nearshore water, insular
   shelf, and shelf slope.
4. Two broad submerged Gaussian ridges that cannot become land.

There is no high-frequency seabed noise. Surface systems see depth only through
`IOceanEnvironment`, and `WaveDerived.EffectiveDepth` stops increasingly deep terrain
from affecting a wave once the depth exceeds that packet's useful scale.

## Shelf-driven rocks

Rock-cluster centers are accepted only in shallow water with a meaningful depth gradient.
Each center emits a compact group; a sparse contour sweep can connect some groups into
reef-like lines. Placement is deterministic for the environment seed.

The enlarged map produces more rocks than Batch 5, so `OceanEnvironment.FindRock` uses
an 8-unit deterministic spatial grid. Simulation identity remains the index in the
authoritative rock list; the grid is only a lookup acceleration structure.

## Target ownership and lifecycle

`TargetMarkerSystem` owns:

- target position;
- visit radius;
- enabled state;
- visit and relocation counts; and
- its deterministic random stream.

`WaveSimulation` exposes copies of `TargetMarkerData` and explicit mutation methods. The
presentation cannot rewrite the target struct directly.

During Apply, after authoritative boat motion is resolved, the target system checks only
the player boat. An arrival increments the counter, relocates the marker, and appends one
`TargetVisited` event. Events become public only after the Apply phase completes.

## Safe relocation

A target candidate must:

- remain inside the world with a fixed edge margin;
- have navigable depth at its center;
- have a ring of navigable samples around it;
- clear all rocks by the configured safety radius; and
- be at least the configured minimum distance from the player.

Random candidates are attempted first. A deterministic grid scan provides a fallback, so
an unlucky random sequence does not leave a visited target sitting under the boat.

## Determinism and replay

The state hash includes all target state plus the target RNG state. Target operations are
deterministic when the same operations occur at the same points in a run. The existing
recorded replay stream remains specifically a boat-control stream; manual debug actions
such as spawning a wave or relocating the target are not serialized as player controls.

## Presentation

The target is an abstract diamond and proximity ring, not a physical buoy. HUD controls
support enable/hide, relocation, radius adjustment, and counter reset. Keyboard shortcuts
support the common operations.

Full-map scale derives from world dimensions and camera aspect. Follow-camera targets are
clamped so their viewport stays inside the rendered ocean.

## Current limits

- Safe placement proves local clearance, not formal pathfinding connectivity. The current
  analytic map has an open connected ocean and no sealed lakes.
- Target debug operations are deterministic but are not part of the boat-control replay
  file because no replay-file format exists yet.
- Wave fronts still sample environment primarily at their center.
- The 30-wave local figure remains a reference measurement, not an enforced density.

# Batch 15 — Deterministic Spatial Broadphase

## Intent

Batch 15 removes obviously distant interaction work before the planned map expansion. It is
an execution-layer change, not a sailing, wave, map, or content change. Batch 14 behavior,
tuning, event order, and state hashes remain the reference.

## Design

After wave propagation has produced its temporary decisions, `WaveSimulation` rebuilds a
uniform grid containing every active predicted crest section. The grid is dense for the
configured map bounds, but cell lists are created lazily and reused. Each section is inserted
once at the midpoint used by the exact boat-contact equation.

Boat and floating-object systems query nearby grid cells into retained buffers. Candidates
are sorted back into authoritative wave/segment order before the existing exact interaction
equations run. Broad hull samples still produce at most one impulse per crest/boat identity;
floating objects still choose the nearest section of each crest before combining drift.

The environment's existing static rock grid now exposes an optional swept-AABB query. Rock
indices are returned in original order before the unchanged swept-circle contact equation
runs. Custom environments that do not implement this optional interface automatically use
brute force.

`EnableSpatialBroadphase=false` retains complete brute-force wave and rock paths. The enabled
flag and cell size are immutable startup configuration, but they are intentionally excluded
from authoritative hashing because they describe how an answer is found rather than game
state.

## Diagnostics and validation

The F3 overlay reports exact/potential checks for wave/boat, wave/floating-object, and rocks,
plus indexed-section and occupied-cell counts. Editor validation runs identical broadphase
and brute-force simulations for 480 ticks and compares state hashes after every tick. The
swept-rock regression also compares the spatial and brute-force paths through a real impact
and tangential escape.

The validation probe additionally warms the retained containers, observes generation-0
collections for another 240 ticks, and keeps the existing 20, 320, 1,000, and 10,000-front
performance profiles.

Reference results on 2026-08-10:

- 480/480 spatial-versus-brute-force ticks matched exactly;
- wave/boat exact checks were 8,822 of 620,460 potential checks;
- floating-object exact checks were 33,030 of 4,787,580 potential checks;
- the warmed 240-tick probe caused zero generation-0 collections;
- a same-process 320-front sample took 2.766s spatial versus 3.672s brute force; and
- a same-process 1,000-front sample took 3.297s spatial versus 4.531s brute force.

Timing is diagnostic rather than a universal hardware guarantee. Equality of final hashes is
the correctness gate; the comparison samples ended at identical hashes.

## Scope boundary

This batch does not skip wave propagation, lower update frequency, stream regions, change
wave identity, or introduce multiplayer replication. Every active crest section still makes
one deterministic propagation decision every fixed tick. Those are later scheduling choices
only if exploration-scale measurements justify them.

## Next gate

Batch 16 should expand the ocean gradually and derive the initial ordered phase count from
physical span and swell period rather than simply multiplying a fixed density. Continental
and insular shelves should remain legible, and traversal time, visual emptiness, source
cadence, object usefulness, and stress cost should all be measured before another expansion.

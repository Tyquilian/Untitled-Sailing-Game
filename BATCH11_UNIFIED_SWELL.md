# Batch 11 Unified Ocean Swell

## Purpose

Batch 11 tests whether the existing discrete segmented model can read as one wide ocean.
It does not use an analytic height field. A single persistent generator emits ordered,
map-spanning phase fronts from the western boundary.

## One normal-ocean source

The source registry still contains western, northern, and southern definitions. Only the
western source is enabled in the normal scenario. Disabled sources have zero weight, no
swell system, no packet count, and `ulong.MaxValue` as their emission schedule. This keeps
the future storm/scenario seam without allowing invisible cross-seas into ordinary play.

The active source owns one `SwellSystemData`. Packet spacing is derived from its fixed phase
period and deep-water speed. Initial placement reconstructs an already-running sequence;
runtime emission adds one boundary front per phase without waiting for population loss.

## Map-spanning fronts

The generator computes the map’s projection onto the crest axis:

`crossSpan = 2 × (abs(axis.x) × halfWidth + abs(axis.y) × halfHeight)`

Natural crest length is that span plus a small overdraw margin. This prevents slightly
rotated crests from exposing artificial gaps at the world edge. The reference map produces
an average 269.41-unit crest across its 250-unit north/south span.

Segment target spacing increases to 16 units and the ceiling increases to 21. The current
map normally uses about 18 segments per front. This preserves local island occlusion and
shelf deformation while keeping 1,000-front cost within the 30 Hz target.

Forty full-width fronts replace the former 80 partial-width fronts. Front count is not a
measure of ocean coverage: the new playable profile owns slightly more total crest sections
while producing a much more ordered composition.

## Breaking debris impulse

Batch 10 treated breaking water as a multiplier on continuous drift. Batch 11 adds stable
contact identity through `LastBreakingWaveId`. When an object first encounters a new
breaking crest:

1. the strongest nearby breaking section supplies direction and force;
2. cargo or radius-scaled wreckage inertia converts force into impulse;
3. a small object/wave-ID-derived angle adds deterministic scatter;
4. velocity is capped by `FloatingObjectMaximumSpeed`; and
5. `FloatingObjectHitByBreakingWave` records the event.

The same crest cannot kick an object every tick. A later crest has a different stable wave
ID and may deliver another impulse. Traveling drift remains continuous and substantially
weaker.

New tuning values included in the state hash:

- `BreakingFloatingObjectImpulse`
- `WreckageInertiaScale`
- `FloatingObjectMaximumSpeed`

`LastBreakingWaveId` is also authoritative and hashed.

## Performance interpretation

Full-width fronts make “wave count” a harsher workload than in Batch 10. The 1,000-front
profile represents roughly 18,000 detailed sections and reaches 35.8 authoritative
ticks/second. The enlarged 10,000-front diagnostic is capped at 21 sections per front and
reaches 2.3 ticks/second.

The next optimization should not reduce normal crest identity. It should schedule distant
sections at lower deterministic frequency while retaining full-rate sections around boats,
breaking zones, objectives, and relevant coastlines.

## Deferred

- active cross-seas, storms, and weather transitions;
- spatial/multi-rate updates;
- larger maps and streaming;
- analytic generation;
- large-vessel hull profiles;
- ports, forts, combat, inventory, and progression.

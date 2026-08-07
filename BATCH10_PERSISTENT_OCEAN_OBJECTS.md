# Batch 10 Persistent Ocean and Floating Objects

## Intent

Batch 10 asks how far the current discrete segmented-wave architecture can go before an
analytic ocean is necessary. It does not begin an analytic migration. It turns source
generation into a continuous ocean service, adds one general-purpose world-object service,
and records an honest 10,000-front scaling result.

## Persistent source model

Each boundary source owns one long-lived `SwellSystemData` stream. The stream defines shared
direction, base energy, packet spacing, mean packet length, mean crest length, and phase
period. Initial fronts are distributed as the already-running state of those streams.

At runtime, each source advances independently. When its `NextEmissionTick` arrives, it
attempts to emit one broad front at the boundary and schedules the next phase whether or not
the world has temporary capacity. This prevents deferred phases from accumulating into a
burst. A 10% population headroom allows normal entry before old fronts leave; a 97% safety
floor is deliberately separate and exists only to recover from unusual mass loss.

The generator is global. It does not spawn around the player and does not require a camera,
boat, objective, or rendering state. Analytic or storm-driven generators could later feed
this source boundary without replacing local segmented propagation.

## Breaking and shelf feedback

`WaveDerived` still computes amplitude, steepness, and force from energy, depth, and packet
length. Traveling segments now begin breaking when either:

- `Steepness >= BreakingSteepness`; or
- `Amplitude / EffectiveDepth >= DepthLimitedBreakingRatio`.

The default depth-limited ratio is `0.30`. This intentionally favors readable outer-shelf
breaking over strict coastal-wave fidelity. It does not add a pre-break simulation state.
Presentation derives a subtle shoaling trace directly from sampled depth, while stronger
bathymetry color bands make the continental and insular shelves easier to read.

## Floating-object service

`FloatingObjectSystem` is authoritative and deterministic. It owns lightweight entities
with stable IDs, type, previous/current position, velocity, collision radius, value, and
active state. It samples nearby authoritative wave decisions, applies drag, rejects land and
rock motion, resolves boat interaction, and emits typed simulation events.

The first two data-driven behaviors are intentionally small:

- cargo is removed and counted when a boat reaches it; and
- wreckage drifts, collides, and exchanges a modest impulse with the boat.

The service can later support supplies, flotsam, mines, mission items, or debris without
making any of those product decisions now. It is not an inventory, economy, combat, port,
or progression system.

## 10,000-front diagnostic

The large-world profile uses an 1,800×1,000 map, 10,000 fronts, no floating objects, and no
energy/breaking/spent decay during its 30 measured ticks. Removing lifecycle retirement is
intentional: the diagnostic measures the full update workload instead of allowing thousands
of initially shallow fronts to disappear before measurement completes. Bathymetry sampling,
shoaling speed, refraction, breaking-state decisions, segment coherence, boat interaction,
source scheduling, and state hashing remain active.

The current result is about 93,000 segments at 5.1 authoritative ticks per second. This
proves the data model can represent and deterministically advance the workload, but the
current all-waves/every-tick scheduler is not a real-time 10k solution.

The next scale experiment should preserve one canonical simulation while separating update
frequency by spatial interest. Nearby interaction fronts remain full-rate; distant fronts
advance through deterministic coarse steps or lower-frequency batches. Multiplayer would
later add authority and replication on top of that same boundary, not change the meaning of
a wave or floating object.

## Tuning controls

New authoritative `SimulationConfig` values included in the state hash:

- `DepthLimitedBreakingRatio`
- `ContinuousWavePopulationHeadroom`
- `WavePopulationSafetyFloor`
- `InitialFloatingObjectCount`
- `FloatingObjectWaveResponse`
- `FloatingObjectDrag`
- `CargoCollectionRadius`
- `WreckageBoatForce`

Existing Batch 9 traveling-contact controls and Batch 8 segment-coherence controls remain
independent.

## Deferred decisions

- analytic or spectral ocean generation;
- spatial interest and multi-rate updates;
- world streaming and persistence;
- multiplayer authority, rollback, and replication;
- inventories, cargo economy, ports, docks, naval forts, combat, and factions;
- large-vessel profiles and broad-hull sampling; and
- storm-authored swell injection.

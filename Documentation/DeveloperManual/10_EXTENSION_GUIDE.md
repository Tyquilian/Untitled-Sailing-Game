# Extension Guide

## General checklist for authoritative features

Before adding a feature, identify:

1. Its authoritative data owner.
2. Whether the data is persistent or one-tick temporary.
3. The Observe/Decide/Apply phase in which behavior belongs.
4. Which existing systems may read it.
5. Which service alone may change it.
6. Events or commands crossing subsystem boundaries.
7. Random-stream ownership and deterministic ordering.
8. State-hash and replay implications.
9. Presentation that observes it without feeding back.
10. Focused validation and performance coverage.

Do not place persistent gameplay state in `WavePrototypeApp`, use render-frame time in the
simulation, or let one entity retain a direct reference to another.

## Adding a configuration field

1. Add the field and default to `SimulationConfig`.
2. Decide whether it is construction-only or safe to change between ticks.
3. Add it to `WaveSimulation.MixConfig` in deterministic order.
4. Pass/read it only in the system that owns the behavior.
5. Add a probe showing the field has the intended effect.
6. Re-run all scale profiles; configuration can affect hash and workload.

Do not rely on public mutability as proof a value is safe to change at runtime. Propagation
retention factors and generated environment/source data are cached during construction/reset.

## Adding authoritative entity data

1. Add the field to the appropriate data structure.
2. Add a corresponding temporary decision field if it changes during ticks.
3. Initialize it in every creation path, including manual and validation paths.
4. Copy it during Apply.
5. Include it in state hashing.
6. Update reset and lifecycle removal behavior.
7. Update presentation snapshot logic only if it requires interpolation.
8. Add initialization, mutation, and deterministic regression checks.

Arrays inside value structs are references. Never expose a `WaveData` copy and assume its
`Segments` can be safely modified by consumers.

## Adding a simulation system

1. Create it in the Simulation assembly with constructor-injected config/environment/services.
2. Decide which authoritative lists it observes and which decision buffers it writes.
3. Instantiate it in `WaveSimulation.Reset` after its dependencies exist.
4. Insert its Decide call at the correct position in `WaveSimulation.Step`.
5. Commit its persistent changes only in `WaveSimulation.Apply` or a coordinator-called Apply
   method.
6. Publish typed `SimulationEvent` values instead of direct cross-system callbacks.

Changing system order is a behavior change. For example, moving floating-object Decide after
boat motion would delay wreckage force by one tick unless the data flow were redesigned.

## Adding a wave source or storm

The source architecture already supports multiple definitions, but Batch 13 normal play
enables only one.

1. Add or enable a `WaveSourceData` definition in `WaveSourceSystem.Reset`.
2. Give it an explicit stable ID, boundary, direction, spread, weight, energy range, and
   period range.
3. Let `EnsureContinuousStreams` create one `SwellSystemData` for it.
4. Use scheduled phase emission; never refill according to missing population.
5. Ensure initial-world allocation and source midpoint are valid for its direction.
6. Confirm map-spanning crest projection and section count.
7. Include any new source fields/state in hashing.
8. Add cadence, attribution, deterministic, overlap-force, breaking, and performance tests.
9. Add diagnostics and an explicit scenario owner for temporary activation.

A traveling storm should preferably create or activate a bounded source/system that emits an
ordered finite train. Randomly spawning independent local waves would reintroduce the visual
disorder removed in Batches 11–13.

## Adding a vessel profile

Batch 13 has no profile identifier, hull dimensions, or multi-point wave sampling. A proper
larger-vessel feature should not merely increase `BoatData.Mass`.

Recommended sequence:

1. Add stable profile data containing mass, hull length/beam, collision shape, propulsion,
   turning, drag, health, and response scales.
2. Store a profile ID or immutable selected values in authoritative boat state.
3. Make boat creation choose a profile explicitly while preserving the current craft.
4. Replace point-scale wave sampling with deterministic hull sample points such as bow,
   center, and stern.
5. Aggregate all samples from one crest into one contribution before applying force/yaw.
6. Expand swept collision deliberately; do not continue using a small circular radius for a
   visually large hull without documenting that approximation.
7. Include profile state/config in hashing and replay setup.
8. Validate ordinary passage, breaker yaw/damage, surfing, grounding, rocks, and performance
   for every profile.

Controls should remain the same unless a separate product decision authorizes complexity.

## Adding a floating-object kind

1. Add the enum value and define its radius, value, and spawn distribution in `Add`.
2. Define continuous drift and breaking inertia behavior.
3. Define boat contact and terrain behavior in `Decide`.
4. Define lifecycle/removal accounting in `Apply`.
5. Add a typed event if another system/presentation must observe it.
6. Add all new data to hashing.
7. Update rendering and HUD separately.

Avoid turning the generic service into an inventory, economy, or combat system unless that
product scope is explicitly chosen.

## Adding an event

1. Add a `SimulationEventType` value without reordering existing values if serialized numeric
   compatibility ever becomes relevant.
2. Specify the meaning of `WaveId`, `BoatId`, `SegmentIndex`, `ObjectId`, `Position`, and
   `Magnitude` for that event.
3. Append it to `pendingEvents` during Decide or Apply.
4. Never use events to mutate authoritative state later in the same tick.
5. Add it to hash expectations automatically through the existing event loop.
6. Update presentation filtering and validation counters where relevant.

## Adding or replacing an environment

Implement `IOceanEnvironment` and optionally provide it through an
`IOceanEnvironmentFactory` passed to `WaveSimulation`.

Required semantics:

- depth is deterministic and meaningful for positions used by systems;
- land and depth threshold agree;
- gradient direction matches increasing depth because propagation bends toward `-gradient`;
- `Rocks` ordering is stable;
- `FindRock` returns the same index for the same state/query; and
- queries do not mutate world/entity state.

After changing world dimensions or bathymetry, retest target placement, initial boats, rock
count, source paths, phase capacity, crest cross-span, shelf breaking, shadowing, camera fit,
and performance.

## Enlarging the world

Current analytic bathymetry stretches with world extents. If the design needs genuinely new
geography, introduce map/profile inputs instead of assuming size alone creates exploration.

Natural initial front count should eventually derive from source period, propagation speed,
usable longitudinal travel span, and scenario duration rather than a manually selected
constant. Do not enforce a population refill merely to maintain a visual density number.

Before increasing toward real-time 10,000-front worlds, introduce deterministic spatial or
multi-rate scheduling. Full rate is needed around boats, floating objects, breaking zones,
shelves, and relevant coastlines; quiet deep-water fronts can be candidates for coarse,
scheduled advancement. Distant state must remain canonical so a future player does not enter
an area whose ocean was generated around them.

## Adding presentation

Presentation can safely consume:

- read-only authoritative snapshots;
- derived query results;
- public events; and
- render-frame time for interpolation or cosmetic animation.

It must not write interpolated transforms back, modify segment arrays, influence random
streams, or use frame time for gameplay. Any camera shake, spray, lean, pitch, or hull-shadow
animation belongs here and should disappear without changing hashes.

## Adding validation or a build batch

Use a small result struct when a probe has several outputs. Configure minimal worlds with
`TargetWaveCount = 0` and custom environments when unrelated waves/objects would obscure the
behavior. Pair identical simulations when deterministic behavior is in question.

For a new packaged batch:

1. Add a named public build entry point.
2. Run current full validation first.
3. Build into a new `Builds/BatchN` folder.
4. Run smoke, graphical frame, and preview capture checks.
5. Record results in the batch note and current README.
6. Commit source/docs separately enough that behavior changes remain reviewable.

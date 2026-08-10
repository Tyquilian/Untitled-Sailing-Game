# Batch 14 Vessel Profiles and Broad-Hull Interaction

## Purpose

Batch 14 tests whether the environmental simulation can support meaningfully larger ships
without turning sailing into a more complicated control scheme. It adds no trim, bracing,
crew, ballast, or stance actions. Both profiles use the existing arcade controls.

## Profile model

Each `BoatData` stores a small `VesselProfileId`; the immutable simulation configuration owns
the corresponding `VesselProfileDefinition`. A definition supplies mass, rendered dimensions,
collision radius, sample count, propulsion and turn scaling, cruise and surf scaling, drag,
wave force and yaw response, and damage scaling.

The original boat is now the `ArcadeSkiff` profile with the exact Batch 13 values. This keeps
the previous feel as the control case rather than retuning it incidentally.

The `HeavyCutter` is intentionally a strong comparison:

- mass `24.0` versus `7.2`;
- length `6.2` versus `2.95`;
- beam `2.8` versus `1.64`;
- collision radius `1.5` versus `0.72`;
- five hull samples versus one; and
- lower acceleration, turn response, cruise/surf limits, wave yaw, and damage taken.

The cutter's larger wave-force scale represents a larger area exposed to water. Its much
larger mass still produces lower acceleration, so scaling the vessel up does not make it a
point boat with merely reduced numbers.

## Broad-hull sampling

The cutter samples center, bow, stern, port, and starboard. Wave interaction searches all
samples and all active sections of one crest, selects the single closest normalized contact,
then contributes one force, yaw, damage calculation, and `WaveHitBoat` event for that crest.
Additional samples expand spatial coverage; they never multiply force.

The same samples check land grounding. Swept rock contact remains circular but uses the
profile's larger radius. Cargo and wreckage also use the active vessel radius.

## Debug comparison

- `Y` changes the player between skiff and cutter.
- `B` spawns a skiff at the cursor.
- `Shift + B` spawns a cutter.
- The HUD reports profile, effective cruise/surf limits, and mass.
- `F3` renders the active hull samples.

These are laboratory controls only. They do not establish vessel switching or spawning as
gameplay features.

## Deterministic validation

Validation covers profile/config ownership, handling contrast, broad-only wave contact,
one-event crest identity, broad grounding, breaker resistance/inertia, and a matching 120-tick
heavy-vessel run. Existing passage, surfing, breaking, rock sweep, replay, source cadence,
and performance probes continue to pass.

The reference profile measurements are:

- 90-tick speed: skiff `10.45`, cutter `7.47`;
- 30-tick turn: skiff `66.6°`, cutter `37.7°`;
- breaker damage: skiff `5.882`, cutter `3.193`; and
- breaker displacement: skiff `2.79`, cutter `1.51`.

## Deliberate limits

- There is no arbitrary-size runtime vessel authoring service yet; two immutable profiles are
  enough to validate the architecture and feel.
- Land uses five representative samples rather than polygon collision.
- Rock contact uses a swept circle rather than an oriented hull polygon.
- Wave loading selects the strongest point from one crest rather than integrating pressure
  over the hull. This is deliberate arcade behavior and preserves crest identity.
- Profiles are fixed after simulation construction, like the rest of the runtime config.

The next scale step should be deterministic spatial broadphase work before increasing ocean
area. The five-sample cutter is inexpensive at playable density, but multiplying all-wave and
all-rock searches across more vessels and a larger map would otherwise compound existing
stress limits.

# Batch 20 - Large-Vessel Foundation

## Intent

Batch 14 proved that the simulation could distinguish the arcade skiff from a 6.2-unit heavy
cutter. Batch 20 takes the next scale step with a merchant-scale hull that is 16.5 units long,
5.2 units wide, and 96 mass units. The purpose is engine capability: large ships can occupy
meaningful water, encounter several parts of the environment at once, and retain inertia
without introducing crew, weapons, inventory, progression, sail trim, or another sailing
action.

## Merchant profile

`MerchantShip` is a third immutable startup profile beside `ArcadeSkiff` and `HeavyCutter`.
Its reference values are:

- mass `96.0`;
- hull `16.5 x 5.2`;
- thirteen representative hull samples;
- `0.92` local rock/object contact radius at each sample;
- cruise and surf scales `0.68 / 0.58`;
- turn scale `0.30`;
- exposed wave-area scale `4.2`; and
- wave-yaw and damage scales `0.22 / 0.25`.

The larger wave-area scale does not make the ship more easily thrown around: mass rises much
faster than exposed area. The resulting ship responds to the same sea but accelerates, turns,
yaws, surfs, and takes damage more slowly.

## Sampled footprint

The merchant uses center, bow, stern, three port/starboard stations, and four shoulder/quarter
samples. All samples are deterministic value offsets derived from the immutable profile.

- Waves search the whole footprint but still choose one closest section and contribute one
  force, yaw, damage calculation, and event per crest/ship identity.
- Land and world bounds test every sample.
- Rock motion sweeps every sample circle over the fixed tick, selects the earliest contact,
  and resolves that one physical encounter.
- Cargo and wreckage use the same footprint, so the bow can collect or strike an object before
  the ship center arrives.
- Spatial wave queries expand by the maximum sample distance, preserving broadphase/brute
  equivalence for long hulls.

This remains representative sampling, not polygonal naval physics or pressure integration.

## Presentation and controls

The merchant renders with a longer multi-triangle hull and two sail planes. Follow-camera
framing automatically widens when the active profile would not fit the current zoom, and the
player highlight encloses the sampled footprint.

Laboratory controls remain deliberately separate from proposed gameplay:

- `Y` cycles skiff, cutter, and merchant;
- `B` spawns a skiff;
- `Shift + B` spawns a cutter; and
- `Ctrl + B` spawns a merchant.

## Deterministic validation

The focused profile reported:

- 90-tick speed `6.15` and 30-tick turn `20.9 degrees`;
- one bow-only wave hit and one center wave hit, never multiplied by thirteen samples;
- one bow-first grounding and one swept bow-rock collision;
- one bow cargo collection where the skiff at the same center position collected none;
- breaker damage/displacement `2.395 / 0.99`, versus cutter `4.010 / 1.77`; and
- 180 matching deterministic merchant ticks.

The full 900-tick reference, replay, cross-sea, shoreline continuity, collision, floating
object, and performance regressions pass. The merchant broadphase and brute-force paths match
for all 480 comparison ticks.

## Packaged verification

The Windows player passed its merchant-profile smoke test at tick 120 while the cross-sea was
entering with 40 pending off-map sections. The final 1,000-front build soak completed 900 ticks
in 28.938 seconds CPU, below the 30-second target. A 1,600 x 900 rendered merchant probe
reported 8.80 ms average, 13.04 ms p99, zero repeated moving frames, and 15,156 dynamic
vertices. Full-resolution preview inspection confirmed that automatic framing shows useful
water around the 16.5-unit hull while preserving a clear size contrast.

## Deliberate limits

- Three immutable profiles are sufficient for this engine gate; arbitrary runtime ship
  authoring remains deferred.
- Hull samples do not rotate continuously during one fixed-tick sweep; they use the decided
  heading for that tick.
- Rock resolution remains circular at each sample, not an oriented polygon or compound Unity
  collider.
- There is no docking, cargo capacity, crew, combat, wake, audio, ship selection screen, or
  class-specific control scheme.

The next planned batch is the Unity world-authoring foundation. It should make bounds,
islands, shelves, source gates, safe regions, and rock-density regions authorable and
previewable by developers without committing to a new designed level.

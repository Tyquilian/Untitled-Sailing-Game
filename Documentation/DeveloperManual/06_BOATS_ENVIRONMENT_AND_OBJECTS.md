# Boats, Environment, Targets, and Floating Objects

## Arcade boat model

Batch 13 has one boat profile. All boats use the same configuration and are point-scale for
wave sampling, with a circular radius for rocks and object contact. `Mass` is stored per boat
but initialized to 7.2 for every boat.

### Wind efficiency and propulsion

Heading is converted to a unit forward vector. Wind efficiency is:

```text
alignment  = dot(forward, normalize(WindDirection))          // -1 to 1
favorable  = clamp01((alignment + 1) / 2) ^ 0.72
efficiency = 0.38 + favorable × 0.62                         // 0.38 to 1
```

The boat can always make forward progress, including directly against the wind. This is an
arcade modifier, not sail trim.

For nonnegative throttle, propulsion is forward × sailing force × throttle × wind efficiency.
It fades smoothly to zero across the configured range below cruise speed. Negative throttle
applies velocity-proportional braking plus weak reverse thrust at 18% of normal sailing force.

### Steering and integration

Steering authority ranges from 32% at rest to 100% at speed 5. Environmental yaw and player
steering accumulate in the same `HeadingImpulse`; the result is integrated by `dt`.

Force is divided by mass and integrated into velocity. Velocity is decomposed in the newly
decided forward/side frame. Side velocity receives exponential lateral drag, then the entire
velocity receives linear drag. Speed above the propulsion cruise cap decays toward the cap
at `BoatSurfExcessDecay`, but remains available for wave-assisted surfing. Final speed is
clamped to the greater of cruise and surf caps.

### Traveling and breaking crest contact

For each wave/boat pair, the interaction system examines every active section but selects
only the section with the smallest normalized elliptical distance.

The ellipse uses the section direction as its longitudinal axis and the perpendicular crest
axis laterally:

```text
travelingAlongRadius = packetLength × TravelingLongitudinalScale
                       + TravelingLongitudinalPadding
breakingAlongRadius  = packetLength × 0.62 + BoatInteractionRadius
acrossRadius          = sectionSpan × 0.62 + BoatInteractionRadius
```

The section position is halfway between its authoritative start and proposed end position
for the tick. This reduces temporal misses without modifying the wave.

For a selected section:

```text
proximity = (1 - normalizedDistance) ^ 0.68
impact = interactionForce × proximity × stateMultiplier
```

Traveling state uses `TravelingImpactMultiplier`. Breaking state interpolates from the
traveling multiplier toward `BreakingImpactMultiplier` using a scale from 0.55 to 1 based on
breaking intensity. Spent state uses 0.12.

Traveling impact additionally fades as the boat's velocity with the crest approaches 72%
of crest speed. This lets ordinary swell pass beneath and overtake a stationary/slow boat
instead of carrying it indefinitely.

The decision receives:

- force along the wave direction;
- additional forward assistance when boat heading follows the wave;
- velocity-proportional resistance when heading into the wave;
- signed yaw from the 2D cross product of boat forward and wave direction; and
- damage only for breaking state.

One `WaveHitBoat` event is emitted per overlapping crest/boat/tick. Multiple different crests
remain additive; there is no combined-force ceiling.

### Grounding and rocks

After integration, leaving world extents or entering land produces `BoatGrounded`. The boat
returns to its old position, velocity reverses at 8%, and damage is `0.12 + oldSpeed × 0.16`.

Otherwise the system performs a swept circle against every rock, preventing high-speed
tunneling. It resolves up to four contacts within one tick:

- expand rock radius by boat collision radius;
- solve the earliest segment/circle intersection;
- place the boat just outside the combined radius plus contact skin;
- retain tangential velocity by `RockTangentialRetention`;
- reflect incoming normal velocity by `RockImpactRestitution`; and
- add `0.22 + impactSpeed × 0.34` damage.

Repeated contacts consume the remaining fraction of tick time, allowing tangential escape.
Unlike wave rock queries, swept boat collision currently scans the entire rock list rather
than using the environment's spatial grid.

## Ocean environment

### Cached bathymetry

`OceanEnvironment` constructs a 2-unit grid over the configured world extents and evaluates
analytic depth once at creation. Runtime `SampleDepth` clamps positions to the grid and uses
bilinear interpolation. `SampleDepthGradient` uses central differences at ±0.6 units.

The analytic authoring layout is defined in a normalized 360×200 coordinate space. Larger
world extents stretch those features rather than adding new terrain.

Depth starts at a quiet 11.2-unit basin and takes the minimum contribution from:

- an irregular eastern continental coastline;
- nine rotated elliptical island/shelf profiles; and
- two submerged Gaussian shelf ridges.

Final depth is clamped to 0.08–12. Land is depth ≤0.24.

### Continental shelf

The eastern coast varies with two broad sine functions. Moving oceanward from the coastline:

- 0–12 normalized units transitions from land depth 0.08 to 0.9;
- 12–72 transitions across the broad shelf from 0.9 to 2.8;
- 72–132 transitions down the slope from 2.8 to 11.2.

Transitions use cubic smoothstep.

### Insular shelves

Each island uses rotated elliptical radius. Radius ≤0.78 is land. Radius 0.78–1.08 reaches
0.9 depth, 1.08–1.75 reaches 3.2, and 1.75–2.8 reaches 11.2. Taking the minimum across
overlapping profiles forms island groups and shared shelves.

### Rocks and spatial lookup

Rock generation uses a dedicated deterministic RNG (`seed XOR 0x5A17`). It first finds up to
46 cluster centers in water depth 0.28–3.35 with sufficient gradient and scaled separation.
Each center attempts 7–16 rocks, with spread scaled by map area. Rock radius ranges roughly
0.5–1.62 before a depth-based scale. A 3.4-unit contour sweep then joins some clusters into
reef-like lines until the total reaches 320.

`AddRockIfSeparated` prevents circle overlap with an additional 0.08 gap. After generation,
rock indices are inserted into an 8-unit spatial grid. `FindRock` tests the requested cell and
its eight neighbors, returning the first overlapping list index or `-1`.

## Target marker

The target is an optional objective service, not a physical buoy or collision body. It
tracks one position and a counter.

A safe target candidate must:

- remain within the world with margin `max(7, TargetSafeClearance + 1)`;
- have center depth at least 1.15;
- clear rocks by `TargetSafeClearance`;
- pass twelve ring samples at the clearance radius, each with depth at least 0.55 and
  rock clearance 1.25; and
- when relocating, remain at least `max(TargetMinimumRelocationDistance,
  VisitRadius × 2.5)` from the player.

Relocation attempts 320 random candidates, then scans a deterministic 7-unit grid with an
RNG-derived offset. On player arrival after movement Apply, it records the visited position,
increments the counter, attempts relocation, and emits `TargetVisited`. A relocation failure
does not undo the visit and leaves the old position.

Disabling the target stops arrival checks and presentation but preserves all data.

## Floating-object service

### Initial population

The service uses a separate RNG (`seed XOR 0x71C43`). If `TargetWaveCount <= 0`, initial
object count is forced to zero for minimal validation worlds. Otherwise it attempts the
configured count. Every third index is wreckage; the others are cargo.

The first five objects are attempted near the player using deterministic angle/distance
patterns. Later objects use random world positions. Candidates need terrain/rock clearance
and at least 20 squared units of separation from existing object centers.

Cargo radius is 0.55–0.78 and value is one, with a 22% chance of value two. Wreckage radius
is 0.92–1.45 and value zero. Initial velocity is a random vector within radius 0.08.

### Continuous wave drift

For each wave, `SampleWaveDrift` selects the nearest active proposed section within seven
units. Force receives linear proximity falloff and a state scale:

- traveling: 0.55;
- spent: 0.12;
- breaking: a 0.55–0.9 interpolation driven by intensity.

Per-section force is capped at five, contributions are summed, and final drift magnitude is
capped at eight. The object integrates drift using `FloatingObjectWaveResponse`, then applies
exponential object drag and the maximum-speed cap.

### Breaking impulse

The strongest nearby breaking section is separately selected. If its wave ID differs from
`LastBreakingWaveId`, the object receives one impulse and records the new ID.

Cargo inertia is 0.58. Wreckage inertia is at least one and otherwise
`radius² × WreckageInertiaScale`. A small deterministic angle from object and wave IDs adds
scatter. The event `FloatingObjectHitByBreakingWave` records the impulse. The same wave cannot
kick the same object on every tick, but a later wave can.

### Boat contact and lifecycle

Cargo contact radius combines boat collision radius with `CargoCollectionRadius`. Contact
marks cargo inactive and emits `FloatingObjectCollected`; Apply increments salvage totals
and removes it.

Wreckage contact uses its physical radius. Positive closing speed produces a force and yaw
on the boat decision and opposite velocity change on the wreckage. `BoatHitWreckage` is
emitted only above 0.2 closing speed.

If proposed object motion leaves its radius outside the world, enters land, or overlaps a
rock, the object remains at its old position and reverses velocity at 18%.

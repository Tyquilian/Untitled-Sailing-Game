# Data Model

Unless otherwise stated, positions, distances, radii, lengths, and speeds use abstract
simulation world units. Time-dependent rates are per second and are integrated with
`SimulationConfig.FixedDeltaTime`. Headings and configured angular rates use degrees.

## Enumerations

### `WaveState : byte`

| Value | Meaning |
|---|---|
| `Traveling` | Coherent segment/front moving normally; may shoal and refract |
| `Breaking` | Breaking intensity is at or above the release threshold; coherent energy is being dissipated into foam |
| `Spent` | Segment has reached land or fallen below useful coherent energy; it slows/stops and may retain visible foam temporarily |

The parent wave's state is aggregated from active sections. If any active section travels,
the parent is `Traveling`; otherwise if any breaks it is `Breaking`; otherwise it is `Spent`.

### `WaveSourceKind : byte`

| Value | Batch 13 use |
|---|---|
| `WesternSwell` | Enabled normal-ocean source traveling generally east |
| `NorthernCrossSea` | Registered but disabled; reserved for scenarios/storms |
| `SouthernCrossSea` | Registered but disabled; reserved for scenarios/storms |

### `FloatingObjectKind : byte`

| Value | Behavior |
|---|---|
| `Cargo` | Collected and removed on boat contact; contributes salvage count/value |
| `Wreckage` | Persists, drifts, receives breaker impulses, collides with boats and terrain |

### `SimulationEventType : byte`

| Event | Producer | Meaning and payload convention |
|---|---|---|
| `WaveStartedBreaking` | Propagation | A traveling section encountered a nonzero break request. `WaveId`, `SegmentIndex`, position, and maximum steepness/depth ratio are supplied. |
| `WaveHitRock` | Propagation | A section overlapped a rock. Magnitude is coherent energy lost in the immediate rock absorption. |
| `WaveHitBoat` | Wave/boat interaction | A crest selected one section against one boat. Magnitude is pre-global-scale impact. |
| `BoatDamaged` | Coordinator Apply | Positive accumulated damage committed to a boat. |
| `BoatHitRock` | Boat motion / Apply | Swept collision resolved. Magnitude is total damage for the tick. |
| `BoatGrounded` | Boat motion / Apply | Proposed motion left the world or entered land. Magnitude is total damage for the tick. |
| `WaveExpired` | Coordinator Apply | A wave failed coherent-section retention. Magnitude is its last aggregate energy. |
| `TargetVisited` | Target system | Player entered target radius. Position is the old target; magnitude is the new visit count. |
| `FloatingObjectCollected` | Floating-object system | Cargo touched a boat. `ObjectId` and cargo value are supplied. |
| `BoatHitWreckage` | Floating-object system | Boat/wreckage contact with positive closing speed. Magnitude is closing speed. |
| `FloatingObjectHitByBreakingWave` | Floating-object system | A new breaking wave identity kicked an object. Includes wave, segment, object, and impulse magnitude. |

## Authoritative wave data

### `WaveSegmentData`

An ordered, locally authoritative section of one broad crest.

| Field | Meaning |
|---|---|
| `Index` | Stable zero-based position in the parent segment array |
| `PreviousPosition` | Position before the latest Apply; used for render interpolation and hashed |
| `Position` | Current authoritative world position |
| `TravelDirection` | Normalized local direction of propagation |
| `Energy` | Local coherent energy, clamped only at creation and reduced thereafter |
| `Speed` | Local forward speed |
| `SampledDepth` | Cached environment depth from the last staggered sample |
| `DepthGradient` | Cached local depth gradient; zero in sufficiently deep water |
| `BreakingIntensity` | Smoothed normalized breaker severity, normally 0–1 |
| `FoamEnergy` | Non-coherent visual residue; it does not apply independent force |
| `State` | Local traveling, breaking, or spent state |
| `Active` | Whether the section continues to participate in propagation, aggregation, interaction, and rendering |

### `WaveData`

One crest identity. Natural waves are broad map-spanning fronts; legacy manual waves may be
short and contain one section.

| Field | Meaning |
|---|---|
| `Id` | Stable wave identity, unique during one reset |
| `SourceId` | Creating source; zero for manual/local debug waves |
| `SwellSystemId` | Owning persistent swell system; zero for manual/local debug waves |
| `Position` | Average position of active sections after aggregation |
| `TravelDirection` | Normalized aggregate direction |
| `Energy` | Average energy of active sections |
| `Speed` | Average speed of active sections |
| `PacketLength` | Longitudinal energy-packet scale used by speed, steepness, contact, and rendering |
| `CrestLength` | Intended total lateral span from first to last section |
| `State` | Aggregate state |
| `Segments` | Allocation-free read-only `WaveSegmentCollection` over the fixed ordered sections |

Inactive segments remain in internal authoritative storage. The storage does not subdivide
or compact, and callers receive section value copies rather than its mutable array. A wave is
removed when active sections fall below the configured coherent fraction.

### `WaveSourceData`

Configuration plus runtime counters for one boundary generator.

| Field | Meaning |
|---|---|
| `Id` | Stable source ID; built-ins use 1–3 |
| `Kind` | Western, northern cross-sea, or southern cross-sea |
| `Enabled` | Whether the source owns a stream and scheduled emission |
| `SegmentStart`, `SegmentEnd` | Boundary line used to determine the source midpoint |
| `Direction` | Base normalized propagation direction |
| `DirectionSpreadDegrees` | Maximum authoring spread; the continuous stream uses 28% at creation and tiny sinusoidal phase variation |
| `SelectionWeight` | Initial count allocation weight among enabled sources |
| `MinimumEnergy`, `MaximumEnergy` | Stream base-energy selection range |
| `MinimumSpacing`, `MaximumSpacing` | Legacy fields retained and hashed; both are zero in Batch 13 built-ins |
| `MinimumPackets`, `MaximumPackets` | Legacy set-size fields; both are one in Batch 13 |
| `SpawnedTrains` | Runtime emission/debug counter; seeded fronts do not consistently increment this historical term |
| `SpawnedPackets` | Count used as runtime phase index and source diagnostic |
| `SpawnedSystems` | One for the enabled source after population; zero for dormant sources |
| `MinimumCalmSeconds`, `MaximumCalmSeconds` | Period-selection range despite the historical `Calm` name |
| `NextEmissionTick` | Authoritative next phase; `ulong.MaxValue` for dormant sources |

### `SwellSystemData`

The persistent shared description for the fronts emitted by one source.

| Field | Meaning |
|---|---|
| `Id` | Stable system identity |
| `SourceId` | Owning source |
| `Direction` | Canonical system direction |
| `BaseEnergy` | Deterministically selected base energy |
| `PacketSpacing` | Deep-water speed multiplied by period |
| `MeanPacketLength` | Base longitudinal packet scale before ±3% per-front variation |
| `MeanCrestLength` | Cross-map span plus overdraw before ±3% per-front variation |
| `CalmGapSeconds` | Historical name for the authoritative phase period |
| `InitialPacketCount` | Fronts reconstructed at reset |
| `EmittedPacketCount` | Initial plus successful scheduled/debug system-front emissions |
| `ActivePacketCount` | Recounted from current wave ownership during source maintenance |
| `BornTick` | System creation tick; Batch 13 continuous streams are born at tick zero |

## Boat, target, and floating-object data

### `BoatData`

| Field | Meaning |
|---|---|
| `Id` | Stable identity |
| `Position` | Current world position |
| `Velocity` | World-space velocity |
| `Heading` | Forward direction in degrees; 0° is positive X |
| `Health` | Hull condition, initialized to 100 and clamped only at a minimum of zero |
| `Mass` | Force divisor; all Batch 13 boats use 7.2 |

### `FloatingObjectData`

| Field | Meaning |
|---|---|
| `Id` | Stable identity |
| `Kind` | Cargo or wreckage |
| `PreviousPosition`, `Position` | Render-interpolated authoritative positions |
| `Velocity` | Current drift velocity |
| `Radius` | Collision/visual size and part of wreckage inertia |
| `Value` | Cargo salvage value; wreckage uses zero |
| `LastBreakingWaveId` | Identity gate preventing the same breaker from kicking the object every tick |
| `Active` | Lifecycle flag; inactive cargo is counted and removed during Apply |

### `TargetMarkerData`

| Field | Meaning |
|---|---|
| `Position` | Current target center |
| `VisitRadius` | Arrival radius, clamped to 2–15 |
| `VisitCount` | Completed player arrivals |
| `RelocationCount` | Successful counted relocations; initial placement is not counted |
| `Enabled` | Whether arrival detection and rendering are active |

## Inputs, events, queries, and derived values

### `BoatControl`

Immutable held input. The constructor clamps throttle to `[-0.35, 1]` and steering to
`[-1, 1]`. Negative throttle brakes and supplies weak reverse thrust.

### `BoatControlCommand`

Immutable tuple of `Tick`, `BoatId`, and `Control`. It is the replayable input unit.

### `SimulationEvent`

Immutable event with `Type`, `WaveId`, `BoatId`, `Position`, `Magnitude`, optional
`SegmentIndex` (default `-1`), and optional `ObjectId` (default `0`). Field meaning depends
on event type; use the event table above.

### `WaveDensitySample`

Immutable diagnostic result containing `WorldCount`, `LocalCount`, sample `Radius`, and
`DesiredVisibleCount`. In Batch 13, `LocalCount` tests parent aggregate positions and is not
a reliable visible-crest measurement.

### `WaveDerived`

Immutable `Amplitude`, `Steepness`, and `Force` calculated from energy, sampled depth, and
packet length. These values are not stored in authoritative wave data. `EffectiveDepth`
caps deep seabed influence at `max(6.5, packetLength × 1.75)` and clamps the lower bound to
0.35.

### `RockData`

Immutable `Position` and `Radius`. The list index is the rock's authoritative identity.

## Temporary decisions

These internal structures are created or reused by systems during a tick and are not public
state after Apply.

| Type | Fields and role |
|---|---|
| `WaveSegmentDecision` | Proposed position, final/coherent direction, speed, energy, cached environment, breaker/foam state, interaction force, state, and active flag |
| `WaveDecision` | Wave ID, aggregate proposed state, expiry flag, active count, and segment-decision array |
| `BoatDecision` | Accumulated force/yaw, proposed transform/velocity, damage, and one collision category |
| `FloatingObjectDecision` | Proposed position, velocity, breaker identity, and active flag |

`SimulationMath.HeadingVector` converts degrees to a unit vector. `SimulationMath.Cross`
returns the scalar 2D cross product used to determine yaw direction.

## Identity and lifetime rules

- Wave, swell-system, boat, and floating-object IDs start at one after reset.
- Source IDs are defined explicitly as 1–3.
- Zero source/system IDs are reserved for manual waves.
- List indices are not stable identities, except rock indices for the immutable environment.
- Removed waves and cargo are physically removed from their lists; IDs are never reused
  before reset.
- Segment indices never change within one wave.

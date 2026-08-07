# Public API and Internal Services

## Supported integration surface: `WaveSimulation`

`WaveSimulation` is the public façade. External gameplay and presentation code should call
it instead of constructing internal systems or modifying entity collections.

### Construction and lifecycle

#### `WaveSimulation(int seed, SimulationConfig config = null, IOceanEnvironmentFactory environmentFactory = null)`

Uses a default mutable `SimulationConfig` and `OceanEnvironmentFactory` when arguments are
null, then immediately calls `Reset(seed)`. A custom environment factory is primarily useful
for tests or future scenario maps.

#### `void Reset(int seed)`

Destroys all current simulation state and reconstructs the world using the existing config
object and environment factory. IDs, ticks, inputs, events, sources, target state, objects,
and boats return to deterministic initial values. External references to prior value arrays
or read-only list contents must be considered stale.

#### `void Step()`

Advances exactly one authoritative tick using the order documented in the lifecycle manual.
It has no render-time dependency. Public `Events` afterward belong to that completed tick.

### Read-only state properties

| Property | Contract |
|---|---|
| `Config` | The active configuration reference. It is publicly mutable in Batch 13, but changing it after construction can invalidate precomputed rates and determinism. Treat it as construction-time configuration. |
| `Environment` | Current environment created during reset |
| `Waves`, `Boats`, `FloatingObjects` | Read-only views of authoritative lists; contained structs are copies, but `WaveData.Segments` exposes the authoritative array reference and must be treated as read-only |
| `Events` | Events from the most recently completed tick only |
| `WaveSources`, `SwellSystems` | Read-only source/system views owned internally |
| `RecordedControls` | Applied tick-addressed boat commands retained in memory |
| `Target` | Value copy of target state |
| `CollectedSalvageCount`, `CollectedSalvageValue` | Cumulative cargo totals since reset |
| `Seed`, `Tick`, `PlayerBoatId`, `SimulatedTime` | Coordinator identity/time state |
| `ActiveWaveSourceCount` | Number of enabled source definitions |
| `PlayerControl` | Currently held control for the player |
| `WindVelocity` | Normalized configured wind direction multiplied by wind speed |
| `TotalWaveSegmentCount`, `ActiveWaveSegmentCount` | On-demand scans over wave arrays |

### Boat API

#### `bool QueueBoatControl(BoatControlCommand command)`

Queues a future/current tick command if the boat exists and the command is not late. Returns
false rather than throwing for an unknown boat or past tick.

#### `void SetPlayerControl(float throttle, float steering)`

Convenience wrapper that queues a clamped control for `PlayerBoatId` at the current tick.
Because held controls persist, calling once continues that control until another command.

#### `int AddBoat(Vector2 position, float heading)`

Adds a stationary 100-health, 7.2-mass boat and returns its new ID. If the requested point is
land or overlaps a rock, the boat-motion service searches concentric integer-radius rings for
nearby water. It does not reject or report relocation. A failed search returns `(0,0)`, which
is used without an additional safety check.

#### `bool ConfigureBoatForValidation(int boatId, Vector2 position, Vector2 velocity, float heading)`

Directly rewrites authoritative transform/velocity for an existing boat. It bypasses terrain
and collision safety and exists for probes; ordinary gameplay should not call it.

### Wave API

#### `void SpawnWave(Vector2 position, Vector2 direction, float energy = 1)`

Creates one manual wave with source/system IDs zero. Packet and crest dimensions derive from
energy and the source RNG. This is the legacy local debug format.

#### `bool SpawnSwellFront(Vector2 position, float energy = 1)`

Creates one natural-format front using the first enabled source's active system direction,
packet scale, map-spanning crest scale, segmentation, source ID, and system ID. It updates
source/system diagnostic counts but does not change the next scheduled emission tick.
Returns false when no enabled source/system is available.

#### `void SpawnWaveForValidation(Vector2 position, Vector2 direction, float energy, float packetLength, float crestLength)`

Creates an exact-size manual wave. Inputs are normalized/clamped by the source service. This
is a validation seam, not normal gameplay API.

#### `Vector2 SampleAmbientWaveField(Vector2 position, float radius = 12)`

For each wave, selects the nearest active authoritative section inside the circular radius,
derives its force, applies linear radial falloff, and sums direction × force. It is a query;
it does not represent the exact boat-contact ellipse or state multipliers.

#### `Vector2 SampleWaveForce(...)`

Source-compatible alias for `SampleAmbientWaveField`, retained for Batch 1–4 consumers.

#### `WaveDensitySample SampleWaveDensity(Vector2 position, float radius)`

Returns total fronts and counts parent aggregate positions inside the radius. It does not
test active segments and therefore under-reports visible map-spanning crests.

### Floating-object and target API

#### `int SpawnFloatingObject(FloatingObjectKind kind, Vector2 position)`

Creates cargo or wreckage only when the point is within world margins, not land, and clear
of rocks. Returns zero on failure or a positive ID on success.

#### `bool RelocateTarget()`

Relocates relative to the current player position using deterministic random attempts and a
grid fallback. Returns false if no safe candidate is found.

#### `void SetTargetEnabled(bool enabled)`

Changes arrival evaluation state. The target retains its position and counters while off.

#### `void SetTargetVisitRadius(float radius)`

Clamps the radius to 2–15 units.

#### `void ResetTargetVisitCount()`

Sets only visit count to zero. Position and relocation count remain unchanged.

#### `bool IsSafeTargetPosition(Vector2 position)`

Exposes the target system's complete center/ring clearance test.

### Queries and diagnostics

`GetWindEfficiency` forwards the arcade wind calculation. Static
`GetWaveSourceLabel(WaveSourceKind)` maps source kinds to display strings. The instance
overload resolves an ID, returning `MANUAL` for zero and `UNKNOWN` for missing IDs.
`CalculateStateHash` returns the full deterministic diagnostic hash.

## Environment service

### `IOceanEnvironment`

| Member | Contract |
|---|---|
| `IReadOnlyList<RockData> Rocks` | Immutable generated rock collection |
| `float SampleDepth(Vector2 position)` | Bilinear cached depth; values outside the world clamp to the grid edge |
| `bool IsLand(Vector2 position)` | True when sampled depth is at or below 0.24 |
| `Vector2 SampleDepthGradient(Vector2 position)` | Central difference at ±0.6 units |
| `int FindRock(Vector2 position, float extraRadius)` | First overlapping rock index found in nearby grid cells, or -1 |

### `IOceanEnvironmentFactory`

`Create(Vector2 worldHalfExtents, int seed)` supplies an environment for a reset. The default
factory constructs `OceanEnvironment`; validation supplies constant-depth and synthetic
island/shelf implementations.

## Internal services

Internal services are constructed only by `WaveSimulation.Reset`. Their methods are
documented to explain behavior, not to recommend direct use.

### `BoatInputBuffer`

- `Reset` clears pending, applied, held input, and cursor.
- `Queue` validates timing/ID shape, replaces duplicate tick/boat entries, or inserts in
  deterministic tick/boat order.
- `BeginTick` advances all due commands into held state and recorded history.
- `GetControl` returns held control or the zero default.

### `WaveSourceSystem`

- Owns source definitions, swell systems, source RNG, and wave/system ID generation.
- `Reset` creates three definitions and enables only the western source.
- `PopulateInitialWorld` creates stream systems, assigns target initial counts by enabled
  weight, seeds phase fronts, uses a diagnostic fallback only at extreme densities, updates
  counters, and schedules the first half-period emission.
- `MaintainPopulation` recounts system activity and emits no more than the scheduled source
  phases. `targetCount` only disables maintenance when nonpositive; it is not a refill target.
- `SpawnSwellFront`, `SpawnManual`, and `SpawnManualForValidation` implement the three wave
  creation formats.
- `DeepWaterCruiseSpeed` is shared with propagation so initialization and later recovery use
  the same speed rule.

### `WavePropagationSystem`

- Constructor precomputes ordinary-energy, foam, and spent retention factors from the
  construction-time config.
- `Decide` resizes/reuses wave decisions, evaluates every section, applies local coherence,
  and aggregates each parent.
- It owns movement, staggered environment sampling, shoaling speed, deep recovery,
  refraction, breaking intensity, coherent loss, foam, rock absorption, land response,
  section activity, and wave retirement decisions.

### `WaveBoatInteractionSystem`

- `Accumulate` clears/resizes boat decisions, then visits every wave/boat pair.
- It selects at most one overlapping section from a crest, calculates traveling/breaking
  response, and adds force, yaw, slowdown, surfing assistance, damage, and one hit event.
- It does not move either waves or boats.

### `FloatingObjectSystem`

- Owns object RNG/IDs and collected totals.
- `Reset` clears and deterministically seeds objects when the wave target is positive.
- `Spawn` validates terrain clearance and creates one object.
- `Decide` calculates drift, identity-gated breaker impulse, boat contact, terrain blocking,
  and object lifecycle decisions. It may add force/yaw to boat decisions.
- `Apply` commits object decisions, removes collected cargo, and updates totals.

### `BoatMotionSystem`

- `Decide` applies held controls, wind efficiency, propulsion fade, braking/reverse, steering,
  accumulated environmental forces, anisotropic drag, surf excess decay, speed cap, world/
  land grounding, and swept rock collision.
- `GetWindEfficiency` returns a forgiving 0.38–1.0 propulsion multiplier.
- `FindNearbyWater` performs a deterministic radial search used by `AddBoat`.

### `TargetMarkerSystem`

- Owns target data and relocation RNG.
- `Reset` initializes counters/radius and performs an uncounted placement.
- `Evaluate` checks only the player after boat Apply, counts arrival, relocates, and emits
  `TargetVisited`.
- `IsSafePosition` checks bounds, center depth, center rock clearance, and a twelve-point
  clearance ring.

### `PrototypeScenario`

`AddInitialBoats` adds the fixed three-boat scenario and returns the first ID as the player.
It is called during reset after boat motion exists and before target/object placement.

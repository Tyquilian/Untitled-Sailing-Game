# Complete Symbol Catalog

This catalog follows the Batch 13 source tree. Data fields and configuration defaults are
expanded in the Data Model and Configuration Reference; this chapter ensures a reader can
locate every declared type and understand every callable member without first opening code.

## `SimulationTypes.cs`

### Types

- `WaveState`, `WaveSourceKind`, `FloatingObjectKind`, `SimulationEventType`: domain enums.
- `WaveSegmentData`, `WaveData`, `WaveSourceData`, `SwellSystemData`, `BoatData`,
  `FloatingObjectData`, `TargetMarkerData`: mutable authoritative value types.
- `WaveDerived`: immutable derived amplitude/steepness/force bundle.
- `SimulationEvent`: immutable one-tick event.
- `BoatControl`: immutable clamped throttle/steering pair.
- `BoatControlCommand`: immutable tick/boat/control tuple.
- `WaveDensitySample`: immutable world/local/reference diagnostic result.
- `SimulationConfig`: mutable default configuration container.

### Functions

- `WaveDerived(energy, depth, packetLength)`: calculates all three derived values.
- `WaveDerived.EffectiveDepth(sampledDepth, packetLength)`: clamps depth to the packet's
  useful influence range.
- `SimulationEvent(...)`: assigns event payload and optional segment/object IDs.
- `BoatControl(throttle, steering)`: clamps input to supported ranges.
- `BoatControlCommand(tick, boatId, control)`: assigns replayable command fields.
- `WaveDensitySample(...)`: assigns diagnostic fields.
- `SimulationConfig.MaximumBoatSpeed.get/set`: legacy alias for `BoatCruiseSpeed`.

## `SimulationDecisions.cs`

### Types

- `WaveSegmentDecision`: one proposed section update, including `CoherentDirection` used
  between the independent and coherence passes.
- `WaveDecision`: proposed parent aggregate and section decisions.
- `BoatDecision`: force/yaw accumulator and proposed motion/damage/collision.
- `FloatingObjectDecision`: proposed object motion, lifecycle, and last breaker ID.
- `SimulationMath`: shared deterministic 2D helpers.

### Functions

- `HeadingVector(heading)`: converts degrees to `(cos, sin)`.
- `Cross(a, b)`: scalar 2D cross product `a.x*b.y - a.y*b.x`.

## `DeterministicRandom.cs`

- `DeterministicRandom(seed)`: initializes xorshift state; zero seed becomes one.
- `State`: exposes current 32-bit state for hashing.
- `NextUInt()`: xorshift32 transition using shifts 13, 17, and 5.
- `Value()`: returns a 24-bit fraction in `[0,1)`.
- `Range(min,max)`: linear float range.
- `InsideUnitCircle()`: angle-uniform, area-uniform disk sample using `sqrt(Value())` radius.

## `BoatInputBuffer.cs`

### State/properties

`pending` stores ordered commands, `applied` stores replay history, `active` stores held
control by boat ID, and `pendingCursor` points to the first unconsumed command.
`AppliedCommands`, internal `PendingCommands`, and `PendingCursor` expose read-only state.

### Functions

- `Reset()`: clears all input state and history.
- `Queue(command,currentTick)`: rejects invalid/late commands; replaces a duplicate or inserts
  in tick/boat order.
- `BeginTick(tick)`: applies and records all due commands.
- `GetControl(boatId)`: returns held control or default zero input.

## `OceanEnvironment.cs`

### `IOceanEnvironment`

- `Rocks`: stable read-only rock list.
- `SampleDepth(position)`: environment depth query.
- `IsLand(position)`: land query.
- `SampleDepthGradient(position)`: local depth-gradient query.
- `FindRock(position,extraRadius)`: overlap query returning index or `-1`.

### `IOceanEnvironmentFactory`

- `Create(worldHalfExtents,seed)`: constructs one environment per reset.

### `OceanEnvironmentFactory`

- `Create(...)`: returns a new `OceanEnvironment`.

### `RockData`

- `RockData(position,radius)`: immutable constructor for the two fields.

### `OceanEnvironment`

Private state contains the rock list/grid, half-extents, and cached depth grid dimensions/
values. `Rocks` exposes the list read-only.

- `OceanEnvironment(worldHalfExtents,seed)`: allocates/builds the depth grid, generates rocks,
  and builds their spatial grid.
- `SampleDepth(position)`: clamps grid coordinates and bilinearly interpolates four samples.
- `EvaluateDepth(position)`: evaluates normalized analytic continent, islands, and ridges.
- `BuildDepthGrid()`: samples `EvaluateDepth` at two-unit grid points.
- `IsLand(position)`: tests depth ≤0.24.
- `SampleDepthGradient(position)`: central difference at 0.6-unit offset.
- `FindRock(position,extraRadius)`: searches the local 3×3 rock-grid neighborhood.
- `GenerateRocks(seed)`: selects gradient/depth-limited clusters and reef-contour samples.
- `BuildRockGrid()`: maps rock indices into eight-unit integer cells.
- `RockCell(position)`: floors world position into a grid key.
- `AddRockIfSeparated(position,radius)`: appends only when clear of all existing circles.
- `Gaussian(point,center,radiusX,radiusY,degrees)`: rotated elliptical Gaussian ridge value.
- `ContinentalShelfDepth(oceanDistance)`: piecewise land/shelf/slope profile.
- `IslandShelfDepth(point,center,radiusX,radiusY,degrees)`: rotated piecewise island/shelf profile.
- `Smooth01(value)`: clamped cubic smoothstep.

## `WaveSourceSystem.cs`

### State/properties

Owns config/environment references, source and swell-system lists, an activity-count scratch
dictionary, one system ID slot per built-in source, RNG, next wave/system IDs, and a
maintenance cursor. Read-only properties expose sources, systems, RNG state, and next IDs.

### Functions

- `WaveSourceSystem(config,environment)`: stores dependencies.
- `Reset(seed)`: resets streams/IDs/RNG and creates the three built-in source definitions.
- `PopulateInitialWorld(waves,targetCount)`: constructs streams, allocates/places initial
  phase fronts, fills only benchmark overflow with fallback fronts, updates counters, and
  schedules first emissions.
- `MaintainPopulation(waves,targetCount,currentTick)`: recounts activity and processes due
  enabled source clocks; it does not refill to `targetCount`.
- `SpawnSwellFront(waves,position,energy)`: attaches one debug front to the first active stream.
- `SpawnManual(waves,position,direction,energy)`: creates a size-derived source-zero wave.
- `SpawnManualForValidation(...)`: creates an exact-size source-zero wave.
- `DeepWaterCruiseSpeed(packetLength)`: shared capped square-root speed function.
- `EnsureContinuousStreams()`: lazily creates one persistent system per enabled source.
- `TrySeedContinuousFront(waves,sourceIndex,packetIndex)`: places an initial half-phase front
  in an available travel slot.
- `TryEmitContinuousFront(waves,sourceIndex,currentTick)`: emits one boundary phase and updates
  source/system counters. `currentTick` is accepted for phase context but not stored in the
  emitted wave.
- `TrySeedHighDensityFallback(waves,sourceIndex)`: adds deep-basin, system-attributed benchmark
  fronts after unique phase capacity is exhausted.
- `UpdateSystemActivity(waves)`: counts current waves by system ID in one pass.
- `AddSystemWave(...)`: clamps/varies dimensions and energy, creates sections, and appends a
  natural attributed front with a new ID.
- `AddManualWave(...)`: derives local packet/crest dimensions from energy.
- `AddManualWaveExact(...)`: normalizes/clamps exact manual inputs and appends a source-zero wave.
- `CreateSegments(...)`: derives section count, positions the crest chain, and samples initial
  environment state.
- `CountWavesInSystem(waves,systemId)`: linear ownership count.
- `FindSystemIndex(systemId)`: linear system lookup.
- `TotalSourceWeight()`: sums nonnegative enabled weights with a 0.001 floor.
- `NextEnabledSourceIndex(cursor)`: deterministic modulo selection among enabled sources.
- `SecondsToTicks(seconds)`: ceiling conversion with a one-tick minimum.
- `DistanceToWorldExit(origin,direction)`: ray distance to the nearest forward world boundary.
- `InsideWorld(position)`: inclusive half-extent test.
- `CreateSource(...)`: builds a source record with kind-dependent energy defaults and legacy
  one-packet fields.
- `Frac(value)`: returns fractional part; unused in Batch 13.
- `DirectionFromDegrees(degrees)`: unit direction constructor.
- `Rotate(direction,degrees)`: normalized 2D rotation.

## `WavePropagationSystem.cs`

Private state stores config/environment/source dependencies and three precomputed exponential
retention factors.

- `WavePropagationSystem(config,environment,sourceSystem)`: stores dependencies and computes
  retention per tick.
- `Decide(waves,decisions,pendingEvents,tick)`: sizes/reuses buffers, decides every section,
  applies coherence, and aggregates every wave.
- `DecideSegment(wave,segment,deepWaterCruiseSpeed,pendingEvents,tick)`: complete local
  propagation, sampling, breaking, foam, contact, and activity calculation.
- `BreakingSeverity(value,threshold)`: maps below-threshold to zero and threshold–1.8× threshold
  into severity 0.22–1.
- `ApplyCoherence(wave,ref decision)`: calculates linked-neighbor direction and interior phase
  correction, then commits proposed coherent directions.
- `CanLink(a,b,maximumLink)`: requires active traveling neighbor inside separation.
- `AggregateWaveDecision(wave,ref decision)`: averages active sections, derives state, and
  decides coherent retirement.

## `WaveBoatInteractionSystem.cs`

- `WaveBoatInteractionSystem(config)`: stores config.
- `Accumulate(waves,waveDecisions,boats,boatDecisions,pendingEvents)`: resets boat accumulators,
  selects one best segment per crest/boat ellipse, applies state/relative-speed response,
  accumulates force/yaw/damage, and emits contact events.

## `BoatMotionSystem.cs`

Private state stores config and environment.

- `BoatMotionSystem(config,environment)`: stores dependencies.
- `Decide(boats,decisions,inputBuffer)`: adds propulsion/steering to accumulated environmental
  decisions, integrates/limits motion, and resolves grounding/rock collision.
- `GetWindEfficiency(heading)`: forgiving heading-based 0.38–1 propulsion multiplier.
- `FindNearbyWater(origin)`: searches 29 radial rings ×16 directions for non-land, rock-clear
  placement; returns zero if none.
- `ResolveRockMotion(start,initialVelocity,dt,out position,out velocity,out damage)`: resolves
  up to four swept contacts across remaining tick time.
- `TryFindEarliestRockHit(start,end,out rockIndex,out hitFraction)`: segment-versus-expanded-
  circle quadratic scan over all rocks.

## `TargetMarkerSystem.cs`

Owns config/environment, target RNG, and target data. `Data` returns a copy; `RandomState`
supports hashing.

- `TargetMarkerSystem(config,environment)`: stores dependencies.
- `Reset(seed,playerPosition)`: resets RNG/data and performs initial uncounted relocation.
- `Relocate(playerPosition)`: counted relocation wrapper.
- `SetEnabled(enabled)`: changes active state.
- `SetVisitRadius(radius)`: clamps to 2–15.
- `ResetVisitCount()`: zeroes only visits.
- `Evaluate(boats,playerBoatId,pendingEvents)`: detects one player arrival after movement,
  counts, relocates, and emits.
- `IsSafePosition(position)`: complete bounds/depth/rock/twelve-point ring test.
- `TryRelocate(avoidPosition,countRelocation)`: 320 random attempts followed by deterministic
  seven-unit grid fallback.

## `FloatingObjectSystem.cs`

Owns config/environment, object RNG/next ID, and collected count/value.

- `FloatingObjectSystem(config,environment)`: stores dependencies.
- `Reset(seed,objects,playerPosition)`: clears state and seeds the cargo/wreckage mix.
- `Spawn(objects,kind,position)`: safe-placement gate followed by `Add`.
- `Decide(objects,waves,waveDecisions,boats,boatDecisions,decisions,pendingEvents)`: computes
  wave drift, new-breaker impulses, cargo/wreckage contact, drag, speed cap, and blocked motion.
- `Apply(objects,decisions)`: removes/counts cargo and commits surviving object state.
- `SampleWaveDrift(position,waves,waveDecisions)`: one nearest section per wave, summed capped
  drift, and strongest breaker identity/result.
- `TryFindSpawnPosition(objects,playerPosition,index,out position)`: deterministic near-player
  candidates for first five, random world candidates later, with clearance/separation.
- `IsSafe(position,clearance)`: bounds, land, and rock test.
- `Add(objects,kind,position)`: creates randomized radius/value/initial velocity and new ID.
- `FloatingWaveSample`: private immutable drift/breaker result with an assigning constructor.
- `Rotate(direction,degrees)`: safe normalized 2D rotation; near-zero input becomes right.

## `PrototypeScenario.cs`

- `AddInitialBoats(simulation)`: creates the fixed three boats and returns the first ID.

## `WaveSimulation.cs`

### State/properties

Private authoritative lists hold waves, boats, floating objects, public/pending events, and
decision buffers. The coordinator owns the input buffer, internal systems, environment
factory, and next boat ID. All public properties are described in the API reference.

### Functions

- `WaveSimulation(seed,config?,environmentFactory?)`: chooses defaults and resets immediately.
- `Reset(seed)`: full deterministic reconstruction.
- `QueueBoatControl(command)`: boat existence and input-buffer gate.
- `SetPlayerControl(throttle,steering)`: current-tick player convenience command.
- `AddBoat(position,heading)`: safe-relocating default-profile creation.
- `ConfigureBoatForValidation(...)`: direct probe-only state rewrite.
- `SpawnWave(...)`: manual local wave wrapper.
- `SpawnSwellFront(...)`: natural active-system front wrapper.
- `SpawnWaveForValidation(...)`: exact manual probe wrapper.
- `SpawnFloatingObject(kind,position)`: object-service wrapper.
- `RelocateTarget()`: player-relative target relocation wrapper.
- `SetTargetEnabled`, `SetTargetVisitRadius`, `ResetTargetVisitCount`,
  `IsSafeTargetPosition`: target-service wrappers.
- `Step()`: executes the complete fixed-tick pipeline and increments Tick.
- `Apply()`: commits boats/damage/collisions, target, objects, waves/expiry, source schedule,
  and public events.
- `GetWindEfficiency(heading)`: boat-service query.
- `SampleAmbientWaveField(position,radius)`: nearest active authoritative section per wave,
  radially weighted derived-force sum.
- `SampleWaveForce(...)`: legacy alias.
- `SampleWaveDensity(position,radius)`: parent-center diagnostic.
- `GetWaveSourceLabel(kind)`: static kind label.
- `GetWaveSourceLabel(sourceId)`: source-ID resolution.
- `FindBoatIndex(boatId)`: private linear lookup.
- `CalculateStateHash()`: complete authoritative FNV-style digest.
- `MixConfig(ref hash)`: folds every config value in fixed order.
- `MixVector`, `MixFloat`, `Mix64`, `Mix`: bit-level hash helpers.

## `WavePrototypeApp.cs`

### Class state

The single `WavePrototypeApp` class groups:

- simulation, camera, meshes, and material;
- accumulator, pause/map/debug/help/target-arrow/test-drive flags, seed, and camera smoothing;
- previous/current wave and boat snapshot dictionaries;
- static/dynamic vertex, color, and triangle buffers;
- frame diagnostic window/results, mesh count, cached hash timing;
- six-entry event log; and
- lazily constructed IMGUI styles.

`Simulation` publicly exposes the coordinator for external inspection.

### Lifecycle, automation, and camera functions

- `Bootstrap()`: creates a persistent app after scene load if absent.
- `Awake()`: initializes simulation, camera, rendering, and startup messages.
- `Start()`: selects smoke, preview, or frame-test command-line mode.
- `CapturePreview()`: automated wait and two camera captures, then exit.
- `RunFrameTest()`: warmup, 600-frame collection/movement/GC metrics, log, exit.
- `CaptureCamera(path)`: render-texture readback and PNG write with resource cleanup.
- `SetupCamera()`: creates and configures the orthographic camera.
- `SetupRendering()`: finds shader/fallback, creates material/mesh objects, snapshots, and
  initial meshes.
- `Update()`: input, fixed-step accumulator, zoom, dynamic mesh, frame diagnostics.
- `LateUpdate()`: interpolated follow/map target and smoothed constrained camera.
- `HandleKeyboard()`: maps all keyboard/mouse-triggered debug operations.
- `TogglePause()`: toggles schedule, clears accumulator, and collapses snapshots.
- `FeedPlayerControl()`: queues automated or keyboard-derived control.
- `ScreenToWorld(screen)`: camera coordinate conversion.
- `GetMapViewSize()`: fits extents to camera aspect.
- `ConstrainCameraTarget(target,size)`: prevents viewport leaving world.

### Simulation-operation functions

- `StepOnce()`: snapshot swap, authoritative step, capture, and player event-log filtering.
- `ResetSimulation()`: reset same seed plus presentation/camera/static reconstruction.
- `SpawnSwellFront(position)`: energy-2.65 natural front and snapshot refresh.
- `SpawnLocalBreakerBurst(position)`: seven energy-varied manual packets.
- `SpawnBoat(position)`: passive default boat and snapshot refresh.
- `SpawnFloatingObject(kind,position)`: object spawn with safe-water feedback.
- `RelocateTarget`, `ToggleTarget`, `ToggleTargetBearing`, `AdjustTargetRadius`,
  `ResetTargetCounter`: HUD/debug operations with correct hash invalidation.
- `PushLog(message)`: timestamps and retains six messages.

### Snapshot and hash functions

- `RenderInterpolationAlpha`: accumulator fraction or one while paused.
- `InitializeSnapshots()`: makes previous/current dictionaries identical to authority.
- `SwapSnapshotBuffers()`: swaps dictionary references without allocation.
- `CaptureCurrentSnapshots()`: repopulates current dictionaries.
- `RefreshSnapshotsAfterExternalMutation()`: captures and supplies previous entries for new
  wave/boat identities.
- `InterpolateWave(current,alpha)`: interpolates parent render copy.
- `InterpolateBoat(current,alpha)`: interpolates boat render copy.
- `GetInterpolatedPlayer(alpha)`: resolves the first/player boat render copy.
- `InvalidateCachedHash()`: forces next HUD hash refresh.
- `GetCachedStateHash()`: calculates no more than four times per unscaled second unless forced.

### Rendering functions

- `RebuildStaticMesh()`: bathymetry and rock geometry.
- `BuildDynamicMesh()`: complete per-frame actor/diagnostic mesh rebuild.
- `AddWaveSourceDiagnostics()`: source boundary and direction geometry.
- `AddSwellStructureBands(alpha)`: derived system extent bands.
- `SwellColor(sourceId,systemId,alpha)`: deterministic diagnostic system color.
- `SourceColor(kind)`: source-kind diagnostic color.
- `AddTargetMarker(target)`: radius ring, pulsing diamond, pointer.
- `AddTargetBearingArrow(boat,target)`: fixed short direction arrow.
- `AddFloatingObject(item,alpha)`: cargo/wreckage geometry and debug velocity.
- `ApplyWorldMeshBounds(mesh)`: manual full-world bounds.
- `RecordFrameDiagnostic(milliseconds)`: 240-sample average/max window.
- `AddBathymetry(vertices,colors,triangles)`: 225×125 colored cell grid.
- `DepthColor(depth)`: piecewise palette.
- `AddWave(...,wave)`: connected section geometry, shoaling, breaking, foam, debug vectors.
- `InterpolatedSegmentPosition(segment,alpha)`: segment position lerp.
- `SegmentColor(state,energy01)`: state/debug energy palette.
- `AddBoat(...,boat,player)`: hull and sail triangles.
- `AddTriangle`, `AddVector`, `AddArrow`, `AddQuad`, `AddRing`, `AddCircle`: low-level mesh
  primitives.

### UI functions

- `InitStyles()`: lazily constructs box/title/label/small/value/button styles.
- `MakeTexture(color)`: creates a hidden 1×1 style background texture.
- `OnGUI()`: complete HUD, controls, source/system data, help, and footer.
- `DrawWaveInspector()`: cursor-nearest active section inspector in debug mode.
- `Format(vector)`: one-decimal coordinate string.

## `BatchBuild.cs`

### Public operations

- `Validate()`: guarded complete validation menu/CLI entry.
- `ValidateBatch9Contact()`: contact-only regression entry.
- `ValidateBatch10Scalability()`: direct 10k progress entry.
- `ValidateBatch13Scalability()`: 320/1,000 report entry.
- `RunValidation()`: full current assertion suite and result logging.
- `BuildBatch3()`: validate, recreate scene, and build to `Builds/Batch3`.
- `BuildBatch4()`: validate, recreate scene, and build to `Builds/Batch4`.
- `BuildBatch5()`: validate, recreate scene, and build to `Builds/Batch5`.
- `BuildBatch6()`: validate, recreate scene, and build to `Builds/Batch6`.
- `BuildBatch7()`: validate, recreate scene, and build to `Builds/Batch7`.
- `BuildBatch8()`: validate, recreate scene, and build to `Builds/Batch8`.
- `BuildBatch9()`: validate, recreate scene, and build to `Builds/Batch9`.
- `BuildBatch10()`: validate, recreate scene, and build to `Builds/Batch10`.
- `BuildBatch11()`: validate, recreate scene, and build to `Builds/Batch11`.
- `BuildBatch12()`: validate, recreate scene, and build to `Builds/Batch12`.
- `BuildBatch13()`: validate, recreate scene, and build to `Builds/Batch13`.
- `BuildWindows()`: alias to Batch 13.

### Result structures

All are private immutable value types whose constructors only assign fields:

- `ImpactProbe`: lateral displacement, heading change.
- `CrestCoverageProbe`: crest length, inside/outside offsets and hit counts.
- `TravelingPassageProbe`: total/max-consecutive contact, breaker events, displacement, peak
  speed, final lead.
- `StateImpactProbe`: displacement, heading change, contacts, breaker events.
- `SegmentOcclusionProbe`: initial/active count, center activity, center lag.
- `ShelfDeformationProbe`: initial/active count and forward spread.
- `SpeedProbe`: pre-impact, peak, and minimum speed.
- `CruiseProbe`: peak/final speed and collision count.
- `RockSweepProbe`: target rock, impact counts, geometry/projection/escape, tunneling and
  determinism flags.
- `PerformanceProbe`: ticks, minimum/final waves, CPU/wall seconds, final hash, finite flag;
  `UpdatesPerSecond` derives ticks/CPU seconds.
- `ReplayProbe`: ticks, commands, original/replay hashes; `Deterministic` compares hashes.
- `TargetProbe`: events/counts/distance/determinism.
- `FloatingObjectProbe`: collection/wreckage results and determinism.
- `OffshoreBreakingProbe`: deep/shelf breaker events and shelf depth.
- `BreakingDebrisProbe`: traveling/breaking speeds, event count, determinism.
- `SourceCadenceProbe`: expected/actual timing, emission/burst/interval/population data.
- `BreakingLifecycleProbe`: energy timeline, foam, events, active count, resumed flag.

### Validation helpers and probes

- `ValidateInitialSwellSystems(simulation)`: source enablement, attribution, first schedule,
  period, cross-span, direction, counters, and periodic projection gaps.
- `ValidateSegmentedWave(wave,config)`: section count/order/activity/clean state/span.
- `RunCruiseProbe()`: no-wave full-throttle speed envelope.
- `RunSweptRockProbe()`: deterministic high-speed collision and escape.
- `CreateRockProbeConfig()`: 0.2-second tick, no waves, 36 speed cap.
- `TryFindRockSweepSetup(...)`: searches generated rocks/directions for a valid tunnel test.
- `EscapeCorridorIsClear`, `SegmentIsWater`, `ClearOfRocks`, `PointInsideWorld`: rock-probe
  geometry filters.
- `SetProbeBoat(...)`: validation state placement and zero control.
- `RunReplayProbe()`: record and replay 360 controls.
- `RunTargetProbe()`: relocation, arrival, disable, reset, and determinism.
- `RunFloatingObjectProbe()`: cargo and wreckage lifecycle/contact determinism.
- `RunBreakingDebrisProbe()`: compares object response to traveling/breaking waves.
- `RunOffshoreBreakingProbe()`: deep control versus shelf-depth break.
- `RunSourceCadenceProbe()`: forced-loss source timing/burst test.
- `RunBreakingLifecycleProbe()`: partial loss, foam, active sections, and resumed travel.
- `AverageActiveSegmentEnergy(wave)`: mean over active sections.
- `RunConstantDepthBreakingProbe(depth)`: breaker event count in synthetic depth.
- `CountPlayerEvents(simulation,type)`: count type for player boat.
- `CountEvents(simulation,type)`: count type globally.
- `RunPerformanceProbe(waveCount,ticks,seed)`: normal/extended ordered workload and process
  CPU/wall measurement.
- `RunLargeWorldPerformanceProbe(waveCount,ticks,seed)`: 1800×1000 10k diagnostic config.
- `IsFinite(Vector2)`, `IsFinite(float)`: NaN/infinity guards.
- `RunImpactProbe(direction,energy)`: side displacement/yaw.
- `RunCrestCoverageProbe()`: one-hit lateral inside/outside test.
- `RunTravelingPassageProbe()`: stationary-hull ordinary crest passage.
- `RunStateImpactProbe(energy,packetLength)`: state-separated encounter.
- `RunSegmentOcclusionProbe()`: synthetic circular-island crest shadow.
- `RunShelfDeformationProbe()`: synthetic cross-crest depth gradient.
- `RunSpeedProbe(direction,energy)`: powered boat following/head-on response.
- `EnsureScene()`: recreates and saves the canonical empty scene.
- `Require(condition,message)`: throws prefixed validation failure.

### Synthetic environment types

- `SegmentProbeEnvironmentFactory`: creates island or shelf probe environment.
- `ConstantDepthEnvironmentFactory`: creates fixed-depth environment.
- `ConstantDepthEnvironment`: no rocks/land/gradient; returns one depth.
- `SegmentProbeEnvironment`: no rocks; either a radius-six island in deep water or a smooth
  Y-axis 11.2-to-1.35 shelf; calculates its own central-difference gradient.

## `WaveVertexColor.shader`

- `appdata`: object-space vertex plus vertex color.
- `v2f`: clip-space vertex plus interpolated color.
- `vert(input)`: transforms position with `UnityObjectToClipPos` and forwards color.
- `frag(input)`: returns vertex color.

The transparent pass disables culling, lighting, and depth writes and uses standard source-
alpha blending.

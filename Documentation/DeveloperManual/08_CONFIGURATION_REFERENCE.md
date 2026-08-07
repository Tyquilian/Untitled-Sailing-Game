# Configuration and Tuning Reference

`SimulationConfig` is a mutable class with public fields. Defaults below describe Batch 13.
Every listed value is mixed into the deterministic state hash. Treat configuration as fixed
after constructing `WaveSimulation`: propagation precomputes retention factors in its
constructor, and changing related fields later would create internally inconsistent behavior.

## Time and world

| Field | Default | Units | Effect |
|---|---:|---|---|
| `FixedDeltaTime` | `1/30` | seconds/tick | Authoritative step duration; source periods are rounded up to ticks |
| `WorldHalfExtents` | `(225,125)` | units | Defines a 450×250 world, source boundaries, environment grid, camera bounds, and expiry |

## Wave propagation and lifecycle

| Field | Default | Units | Effect |
|---|---:|---|---|
| `BaseWaveSpeed` | 8.6 | units/s | Upper bound on deep-water cruise speed |
| `EnergyDecayPerSecond` | 0.012 | exponential rate/s | Ordinary coherent-energy decay |
| `BreakingMinimumEnergyLossPerSecond` | 0.14 | exponential rate/s | Loss at zero end of breaking-intensity interpolation |
| `BreakingEnergyLossPerSecond` | 0.78 | exponential rate/s | Loss at maximum breaking intensity |
| `BreakingIntensityAttackPerSecond` | 4.2 | intensity/s | Speed at which severity rises |
| `BreakingIntensityRecoveryPerSecond` | 0.9 | intensity/s | Speed at which severity falls |
| `BreakingReleaseIntensity` | 0.08 | normalized | Minimum intensity for breaking state and loss |
| `BreakingEnergyToFoam` | 0.72 | fraction | Coherent loss converted to non-force-bearing foam |
| `FoamEnergyLossPerSecond` | 0.75 | exponential rate/s | Foam decay |
| `MinimumFoamEnergy` | 0.018 | energy | Minimum foam able to keep a non-land section active |
| `SpentEnergyLossPerSecond` | 3.2 | exponential rate/s | Terminal land/spent coherent-energy decay |
| `MinimumEnergy` | 0.06 | energy | Minimum coherent energy for normal activity |
| `BreakingSteepness` | 0.34 | ratio | Steepness break threshold |
| `DepthLimitedBreakingRatio` | 0.30 | amplitude/depth | Outer-shelf break threshold |
| `RockInteractionRadius` | 1.15 | units | Wave/rock query scale; propagation passes 30% of it as extra radius |
| `RockEnergyAbsorption` | 0.42 | fraction control | Immediate contact removes this value ×0.32 of coherent energy |
| `WaveRefractionStrength` | 0.16 | blend rate | Lateral bend toward shallow water |
| `WaveShoalingDeceleration` | 7.0 | units/s² | Speed approach toward lower shallow target; also breaking deceleration |
| `WaveDeepRecovery` | 0.72 | units/s² | Traveling speed recovery after entering deeper water |

Increasing energy makes amplitude, steepness, force, and therefore breaking more likely.
Changing packet length simultaneously changes deep speed, steepness, depth influence, contact
length, and visual thickness; it is not a purely visual scale.

## Crest segmentation and coherence

| Field | Default | Units | Effect |
|---|---:|---|---|
| `WaveSegmentTargetSpacing` | 13.5 | units | Desired distance used to derive section count |
| `WaveMaximumSegments` | 20 | count | Hard ceiling for sections per broad crest |
| `WaveEnvironmentSampleInterval` | 4 | ticks | Staggered depth/gradient refresh period |
| `WaveSegmentDirectionCoherence` | 1.4 | rate/s | Neighbor-direction blend strength |
| `WaveSegmentPositionCoherence` | 1.15 | rate/s | Interior longitudinal phase correction strength |
| `WaveSegmentLinkBreakMultiplier` | 1.9 | spacing multiple | Maximum neighbor distance for physics and visual linking |
| `WaveMinimumActiveSegmentFraction` | 0.45 | fraction | Parent retirement threshold |

Smaller target spacing increases local terrain detail only until the maximum-section ceiling.
Higher segment count and shorter environment intervals directly increase CPU cost. The 1,000-
front Batch 13 benchmark has little headroom.

## Wave-to-boat interaction

| Field | Default | Units | Effect |
|---|---:|---|---|
| `BoatInteractionRadius` | 2.35 | units | Padding around the selected crest-section ellipse |
| `WaveBoatForceScale` | 15.5 | force multiplier | Global directional wave force on boats |
| `BreakingImpactMultiplier` | 2.15 | multiplier | Maximum breaking-state impact scale |
| `WaveYawScale` | 25 | angular impulse scale | Signed heading response |
| `TravelingImpactMultiplier` | 0.38 | multiplier | Ordinary traveling-crest impact |
| `TravelingLongitudinalScale` | 0.30 | packet-length fraction | Traveling contact ellipse length |
| `TravelingLongitudinalPadding` | 0.85 | units | Fixed traveling contact padding |
| `TravelingCarrySpeedFraction` | 0.72 | crest-speed fraction | Traveling force fades to zero as boat speed with wave approaches this target |
| `TravelingYawMultiplier` | 0.58 | multiplier | Ordinary-wave yaw relative to breaking/spent calculation |

The system has no global overlap ceiling. Different crests add forces independently. Do not
increase source count or create disordered crossing systems without retesting combined forces.

## Sailing and boat motion

| Field | Default | Units | Effect |
|---|---:|---|---|
| `WindSpeed` | 7.5 | units/s | Diagnostic wind-vector magnitude; propulsion uses direction/efficiency, not this magnitude |
| `WindDirection` | `(0.94,0.342)` | vector | Normalized when used; roughly 20° |
| `SailingForce` | 31 | force | Forward propulsion before throttle, efficiency, and speed fade |
| `BoatCruiseSpeed` | 12.5 | units/s | Propulsion target and surf-excess reference |
| `BoatSurfSpeedCap` | 18 | units/s | Absolute normal profile speed cap |
| `BoatCruisePropulsionFadeRange` | 0.9 | units/s | Smooth propulsion fade interval below cruise |
| `BoatSurfExcessDecay` | 0.22 | exponential rate/s | Natural decay of speed above cruise |
| `BoatLinearDrag` | 0.135 | exponential rate/s | Whole-velocity damping |
| `BoatLateralDrag` | 0.72 | exponential rate/s | Additional side-slip damping in boat-local frame |
| `BoatCollisionRadius` | 0.72 | units | Boat circle for rocks and floating-object contact |
| `RockImpactRestitution` | 0.14 | fraction | Reflected normal velocity after rock impact |
| `RockTangentialRetention` | 0.82 | fraction | Tangential velocity retained after rock contact |
| `RockContactSkin` | 0.025 | units | Separation outside combined collision radius |
| `BoatTurnRate` | 72 | degrees/s at full authority | Player steering scale |

`MaximumBoatSpeed` is a source-compatible property alias that gets/sets `BoatCruiseSpeed`.
It is not the surf cap.

## Population, target, and world objects

| Field | Default | Units | Effect |
|---|---:|---|---|
| `TargetWaveCount` | 20 | fronts | Initial reconstructed front count; nonpositive also disables scheduled maintenance and initial objects |
| `DesiredVisibleWaveCount` | 7 | fronts | HUD diagnostic reference only; no enforcement |
| `DefaultTargetVisitRadius` | 5 | units | Reset radius before clamp to 2–15 |
| `TargetSafeClearance` | 4.5 | units | Target edge/rock/ring safety scale |
| `TargetMinimumRelocationDistance` | 36 | units | Minimum relocation distance before radius-derived minimum |
| `InitialFloatingObjectCount` | 24 | objects | Initial attempted count when `TargetWaveCount > 0` |
| `FloatingObjectWaveResponse` | 0.42 | acceleration multiplier | Continuous wave-drift integration |
| `FloatingObjectDrag` | 0.34 | exponential rate/s | Object velocity damping |
| `CargoCollectionRadius` | 1.15 | units | Added to boat radius for cargo collection |
| `WreckageBoatForce` | 17 | force scale | Wreckage-to-boat contact force |
| `BreakingFloatingObjectImpulse` | 2.15 | impulse scale | Identity-gated breaker kick |
| `WreckageInertiaScale` | 1.65 | multiplier | Converts wreckage radius² into resistance to breaker impulse |
| `FloatingObjectMaximumSpeed` | 9 | units/s | Absolute drift speed cap |

## Coupled tuning warnings

- `TargetWaveCount` is historically named and overloaded: it seeds front count, disables
  source maintenance when nonpositive, and disables initial floating objects when nonpositive.
- `DesiredVisibleWaveCount` is not reliable with map-spanning fronts because the query uses
  parent centers.
- World extents stretch the normalized bathymetry and affect source cross-span/crest length.
  They do not create additional authored geography.
- Changing fixed timestep changes source period rounding, input ticks, all integrations, and
  deterministic hashes.
- Changing config after simulation construction can disagree with cached propagation decay
  factors and already-created environment/source geometry.
- Increasing `WaveMaximumSegments`, source count, world scale, or active front count can push
  the narrow 1,000-front performance margin below real time.

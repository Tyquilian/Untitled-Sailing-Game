# Codex Working Context

This is a compact working reference, not the human developer manual. Current baseline:
Batch 20 plus the post-Batch 13 architecture-hardening pass, Unity `6000.3.2f1`.

## Non-negotiable rules

- Environment is the primary gameplay system.
- Arcade sailing remains immediate; no manual trim, bracing, or unnecessary actions.
- `WaveSimulation` owns authority. Systems Decide; coordinator Apply commits.
- Rendering/interpolation never feeds simulation.
- Energy is master wave quantity; derived amplitude/steepness/force.
- Broad segmented crest contributes once per crest/entity encounter.
- Source clock, not population, controls natural emission.
- Ordinary swell passes a hull; strong sustained response belongs to breakers.
- Normal ocean has one western source; `N` explicitly starts/ends the bounded cross-sea.
- Analytic deep-water generation remains on hold.

## Current baseline

- World 1350×500 (6× Batch 15 area); fixed-scale central geography, a new western island
  chain, and an eastern-boundary continent; 784 rocks.
- Negative `TargetWaveCount` derives ordered startup phases from source travel span / packet
  spacing; zero disables the sea; positive values remain explicit test/stress overrides.
- Seed 1847 derives 59 initial map-spanning fronts; observed 59–64 after one injected front.
- Period 2.3–2.7 s; seed 1847 selects 76 ticks ≈2.53 s.
- Up to 40 sections/front, target spacing 13.5, environment sample every 4 ticks.
- Deep-water decay is 0.0025/s. Fronts retain identity until the final active section; there
  is no age timer or 45-percent group cutoff. Breaking loss is 0.035–0.24/s, and rock
  absorption is timestep-correct.
- One persistent western source/system; two normally dormant cross-sea definitions. Batch 18
  can authorize the northern source as a five-phase event without perturbing the carrier.
- Cross-sea defaults: manual start, 24s build, 60s established, 20s departure, then natural
  drain. Emission scale begins at 0.55; the source keeps its resolved 4.2-6.2s period.
- Directional cross-seas derive a true upstream corner and phase plane from direction plus
  world bounds. Northern entry is northwest; southern entry is southwest.
- Off-map crest sections use `WaveState.PendingEntry`: deterministic motion only, with no
  render, environment sampling, decay, breaking, foam, spatial indexing, force, or density.
  They remain inactive to downstream systems; boundary-aware propagation is enabled only
  after a directional system exists, leaving the normal-ocean hot path unchanged.
- Partial breaking transfers coherent loss to non-force foam and can resume traveling.
- Three initial arcade-skiff boats, optional target, 48 initial cargo/wreckage objects.
- Rock radii at seed 1847 are 0.80–2.86 (1.44 average). Follow-camera zoom spans 10.5–96;
  `M` remains full-map view.
- Immutable arcade-skiff, heavy-cutter, and merchant-ship profiles. The skiff preserves
  Batch 13 values.
- Heavy cutter: mass 24, 6.2 x 2.8 hull, 1.5 collision radius, five hull samples.
- Merchant ship: mass 96, 16.5 x 5.2 hull, thirteen hull samples, and 0.92 local
  rock/object radius. It uses the same arcade controls with slower propulsion and turning.
- Wave and land checks use hull samples but still contribute once per crest/boat identity.
- Rock, cargo, and wreckage contact use profile samples as well. Swept rock motion chooses
  the earliest sample contact; it does not multiply one encounter by sample count.
- A rebuilt-per-tick deterministic grid culls wave-section candidates for boats and floating
  objects. The static rock grid also serves swept boat contact. Exact equations and original
  wave/segment/rock ordering remain authoritative.
- `EnableSpatialBroadphase=false` selects the retained brute-force reference path. Spatial
  execution settings are deliberately excluded from the state hash.

## Known hazards

- `SampleWaveDensity` counts a parent front when an active crest section intersects the view.
- `TargetWaveCount < 0` derives the playable reconstruction; exactly zero disables source
  maintenance and initial floating objects.
- Runtime config is immutable; use a new `SimulationConfig` builder and reconstruct the
  simulation to change startup tuning.
- Recorded boat controls retain the latest 65,536 commands by default; configure nonpositive
  capacity for unlimited diagnostic replay or disable recording when it is unnecessary.
- A dense per-map wave grid is allocated at simulation construction and reuses cell/query
  containers. Out-of-bounds sections clamp to boundary cells, producing false positives but
  never false negatives for in-bounds entities.
- 1,000 and 10,000 fronts remain stress/diagnostic profiles. Broadphase removes distant
  entity-interaction checks, but propagation still updates every section every tick; spatial
  culling alone is not multi-rate simulation or world streaming.
- Batch 15 same-process samples measured spatial/brute CPU time at 2.766/3.672s for
  320 fronts over 300 ticks and 3.297/4.531s for 1,000 fronts over 120 ticks.
- Batch 17 reference: 108s nominal width crossing, local/world fronts 6/64 after 900 ticks,
  1,000-front stress 55 ticks/s, 10k diagnostic 2.3 ticks/s for 30 ticks. Packaged frame
  probe: 8.88ms average / 12.60ms p99 with 15,467 dynamic vertices.
- The long-transit regression front reaches the near eastern shelf at tick 4,803 with 0.187
  energy and expires after terrain contact at tick 5,138.
- Batch 19 focused profile: north entry `(-225,125)`, upstream center `(-118.20,191.07)`,
  first emission/entry ticks 80/97, 38/0 pending/entered and zero interior at emission,
  complete drain tick 2,109, 782 local-overlap ticks, and exact 157-tick cadence. Western
  source clock matched its no-event control throughout.
- Batch 20 packaged stress: 1,000 fronts over 900 ticks in 28.938s CPU (31.1 ticks/s), below
  the 30-second target. The thermal calibration has a 32s floor and a fixed 34s hard ceiling;
  the 320-front run remains independently gated.
- Batch 20 packaged merchant smoke passed with 40 pending sections after the first boundary
  phase. Merchant frame probe: 8.80ms average / 13.04ms p99, 15,156 dynamic vertices, and no
  repeated moving frames.
- Land, waves, rocks, and floating objects use representative hull samples. Rocks use swept
  sample circles rather than an oriented hull polygon.
- Same-build/platform determinism only; replay contains boat controls, not debug operations.

## Commands

Validation execute method: `WavePrototype.Editor.BatchBuild.Validate`.
Current build execute method: `WavePrototype.Editor.BatchBuild.BuildBatch20`.
Player modes: `-smoketest`, `-frametest`, `-capturepreview`.

## Roadmap

1. Playtest Batch 20 merchant scale, inertia, navigation footprint, and sea response.
2. Batch 21: Unity world-authoring foundation for developers—assets, bounds, source gates,
   shelf/island/rock-region handles, gizmos, deterministic preview, and validation. Do not
   author new encounter regions as part of the tooling batch.
3. Product gate: deeper environmental sandbox or minimal cargo/damage/landmark game.

Sea-state direction/orchestration is deferred as game mechanics. Additional level design is
deferred until the Unity authoring workflow is intentionally scheduled and built.

The human developer manual was intentionally removed from the workspace after archival in
Git commit `7172c9c`. Use source, focused batch records, and this compact context for current
work; consult that historical commit only when the archived manual is explicitly requested.

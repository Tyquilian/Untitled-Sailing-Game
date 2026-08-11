# Codex Working Context

This is a compact working reference, not the human developer manual. Current baseline:
Batch 18 plus the post-Batch 13 architecture-hardening pass, Unity `6000.3.2f1`.

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
- Partial breaking transfers coherent loss to non-force foam and can resume traveling.
- Three initial arcade-skiff boats, optional target, 48 initial cargo/wreckage objects.
- Rock radii at seed 1847 are 0.80–2.86 (1.44 average). Follow-camera zoom spans 10.5–96;
  `M` remains full-map view.
- Immutable arcade-skiff and heavy-cutter profiles. The skiff preserves Batch 13 values.
- Heavy cutter: mass 24, 6.2 x 2.8 hull, 1.5 collision radius, five hull samples.
- Wave and land checks use hull samples but still contribute once per crest/boat identity.
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
- Batch 18 focused profile: 58.4-degree crossing; four fronts at exact 157-tick cadence;
  completion tick 1,565; 785 local-overlap ticks; repeat uses fresh system identity. Western
  clock matched a no-event control for all 1,565 ticks.
- Batch 18 packaged active-event frame probe: 8.35ms average / 8.52ms p99, 15,483 dynamic
  vertices, and no repeated moving frames.
- Land and waves use representative hull samples; rocks still use swept profile circles rather
  than oriented hull polygons.
- Same-build/platform determinism only; replay contains boat controls, not debug operations.

## Commands

Validation execute method: `WavePrototype.Editor.BatchBuild.Validate`.
Current build execute method: `WavePrototype.Editor.BatchBuild.BuildBatch18`.
Player modes: `-smoketest`, `-frametest`, `-capturepreview`.

## Roadmap

1. Playtest and tune Batch 18 crossing-system readability and force consequences.
2. Batch 19 candidate: a narrow sea-state director using proven carrier/event vocabulary,
   without committing to a full weather simulation.
3. Later vessel pass: more profiles only after the two-hull playtest establishes useful scale.
4. Product gate: deeper environmental sandbox or minimal cargo/damage/landmark game.

The human developer manual was intentionally removed from the workspace after archival in
Git commit `7172c9c`. Use source, focused batch records, and this compact context for current
work; consult that historical commit only when the archived manual is explicitly requested.

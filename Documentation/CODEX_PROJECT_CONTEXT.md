# Codex Working Context

This is a compact working reference, not the human developer manual. Current baseline:
Batch 16 plus the post-Batch 13 architecture-hardening pass, Unity `6000.3.2f1`.

## Non-negotiable rules

- Environment is the primary gameplay system.
- Arcade sailing remains immediate; no manual trim, bracing, or unnecessary actions.
- `WaveSimulation` owns authority. Systems Decide; coordinator Apply commits.
- Rendering/interpolation never feeds simulation.
- Energy is master wave quantity; derived amplitude/steepness/force.
- Broad segmented crest contributes once per crest/entity encounter.
- Source clock, not population, controls natural emission.
- Ordinary swell passes a hull; strong sustained response belongs to breakers.
- Normal ocean has one western source; cross-seas require an explicit scenario/storm.
- Analytic deep-water generation remains on hold.

## Current baseline

- World 900×500 (4× Batch 15 area); one normalized continent/island shelf map; 640 rocks.
- Negative `TargetWaveCount` derives ordered startup phases from source travel span / packet
  spacing; zero disables the sea; positive values remain explicit test/stress overrides.
- Seed 1847 derives 39 initial map-spanning fronts; observed 37–42 after one injected front.
- Period 2.3–2.7 s; seed 1847 selects 76 ticks ≈2.53 s.
- Up to 40 sections/front, target spacing 13.5, environment sample every 4 ticks.
- One western source/system; two dormant cross-sea definitions.
- Partial breaking transfers coherent loss to non-force foam and can resume traveling.
- Three initial arcade-skiff boats, optional target, 48 initial cargo/wreckage objects.
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
- Batch 16 reference: 72s nominal width crossing, local/world fronts 4/38 after 900 ticks,
  1,000-front stress 59 ticks/s, 10k diagnostic 2.2–2.5 ticks/s for 30 ticks. Packaged
  frame probe: 8.71ms average / 13.53ms p99 with 8,251 dynamic vertices.
- Land and waves use representative hull samples; rocks still use swept profile circles rather
  than oriented hull polygons.
- Same-build/platform determinism only; replay contains boat controls, not debug operations.

## Commands

Validation execute method: `WavePrototype.Editor.BatchBuild.Validate`.
Current build execute method: `WavePrototype.Editor.BatchBuild.BuildBatch16`.
Player modes: `-smoketest`, `-frametest`, `-capturepreview`.

## Roadmap

1. Batch 17: bounded ordered storm/cross-sea event, if the expanded normal ocean playtests well.
2. Later vessel pass: more profiles only after the two-hull playtest establishes useful scale.
3. Product gate: deeper environmental sandbox or minimal cargo/damage/landmark game.

The human developer manual was intentionally removed from the workspace after archival in
Git commit `7172c9c`. Use source, focused batch records, and this compact context for current
work; consult that historical commit only when the archived manual is explicitly requested.

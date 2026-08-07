# Codex Working Context

This is a compact working reference, not the human developer manual. Current baseline:
Batch 13, commit `337f521`, tag `batch-13-baseline`, Unity `6000.3.2f1`.

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

- World 450×250; one normalized continent/island shelf map; 320 rocks at seed 1847.
- Initial 20 map-spanning fronts; observed 15–23 after one injected front.
- Period 2.3–2.7 s; seed 1847 selects 76 ticks ≈2.53 s.
- 20 sections/front, target spacing 13.5, environment sample every 4 ticks.
- One western source/system; two dormant cross-sea definitions.
- Partial breaking transfers coherent loss to non-force foam and can resume traveling.
- Three same-profile boats, optional target, 24 initial cargo/wreckage objects.

## Known hazards

- `SampleWaveDensity` counts parent centers, not visible/nearby sections.
- `TargetWaveCount <= 0` also disables source maintenance and initial floating objects.
- Config is mutable but propagation retention/environment/source data are construction/reset
  cached.
- `WaveData.Segments` exposes a mutable array through read-only list views.
- 1,000 fronts run at only 30.8 ticks/s on reference PC; 10k runs at 2.2 ticks/s.
- Boats are point-scale for waves and circular for rocks; larger vessels need hull sampling.
- Same-build/platform determinism only; replay contains boat controls, not debug operations.

## Commands

Validation execute method: `WavePrototype.Editor.BatchBuild.Validate`.
Current build execute method: `WavePrototype.Editor.BatchBuild.BuildBatch13`.
Player modes: `-smoketest`, `-frametest`, `-capturepreview`.

## Roadmap

1. Batch 14: correct density metric; vessel profiles and broad-hull sampling.
2. Batch 15: gradual exploration-scale map and phase count derived from span/period.
3. Batch 16: deterministic spatial/multi-rate scheduling if scale requires it.
4. Batch 17: bounded ordered storm/cross-sea event.
5. Product gate: deeper environmental sandbox or minimal cargo/damage/landmark game.

Read `Documentation/DeveloperManual/README.md` only when detailed Batch 13 implementation
reference is needed.

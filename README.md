# Tactical Sailing — Deterministic Spatial Broadphase (Batch 15)

Batch 15 prepares the sandbox for gradual ocean expansion by culling distant interaction
candidates. It does not change sailing, wave tuning, map content, vessel profiles, or event
order. A retained brute-force reference path is compared against the spatial path tick by tick.

## Run the latest build

Open `Builds/Batch15/TacticalSailingBatch15.exe` and keep the complete `Batch15` directory
together. Earlier batch builds remain preserved.

The source project targets Unity `6000.3.2f1`. Open
`Assets/WavePrototype/WaveDemo.unity` and enter Play Mode.

## Controls

| Input | Action |
|---|---|
| W / Up | Forward power |
| S / Down | Brake and low-speed reverse |
| A D / Left Right | Steer |
| Y | Switch the player between skiff and heavy cutter (debug) |
| B | Spawn an arcade skiff at the cursor |
| Shift + B | Spawn a heavy cutter at the cursor |
| M | Full map / return to follow camera |
| Mouse wheel | Follow-camera zoom |
| Q | Spawn one natural-format segmented swell front at the cursor |
| Shift + Q | Spawn the seven-packet local breaker burst for comparison |
| T / V / K | Relocate target / toggle target / toggle bearing arrow |
| Left bracket / Right bracket | Adjust target visit radius |
| C / X | Spawn collectible cargo / collidable wreckage |
| P / Period | Pause / advance one simulation tick |
| F3 | Swell, hull-sample, source, foam, object, collision, and frame diagnostics |
| R | Reset with the same deterministic seed |
| H / F1 | Toggle help |
| Escape | Quit |

The `Y` switch and the vessel-spawn controls are comparison tools, not proposed gameplay
actions. With `F3` enabled, orange points show each vessel's authoritative hull samples.

## Batch 15 changes

- Rebuilds a deterministic dense-grid index over active predicted crest sections each tick.
- Culls distant wave/boat and wave/floating-object pairs before running the unchanged exact
  interaction equations in original wave/segment order.
- Reuses the static rock grid for deterministic swept-contact candidates while preserving a
  brute-force fallback for custom environments.
- Retains query buffers and cell lists across ticks and exposes culling counts in the F3
  diagnostics overlay.
- Adds an immutable startup switch for broadphase/brute-force comparison without including
  execution policy in authoritative state hashes.
- Preserves the Batch 14 arcade-skiff and heavy-cutter comparison tools and behavior.

## Validation summary

Reference editor validation passed on 2026-08-10:

- deterministic reference run: 900/900 matching ticks;
- broadphase/brute-force comparison: 480/480 identical ticks;
- wave/boat candidate checks: `8,822 / 620,460` potential;
- floating-object candidate checks: `33,030 / 4,787,580` potential;
- warmed 240-tick spatial probe: zero generation-0 collections;
- same-process 320-front sample: `2.766s` spatial / `3.672s` brute force;
- same-process 1,000-front sample: `3.297s` spatial / `4.531s` brute force;
- deterministic reference run: unchanged `FAB08900B346EEB8` final hash; and
- all prior wave, vessel, collision, replay, source, target, and object regressions passed.

See `BATCH15_DETERMINISTIC_SPATIAL_BROADPHASE.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch15 -quit -logFile build-batch15.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, and full-map PNGs beside the executable.

## Playtest question

Does Batch 15 feel indistinguishable from Batch 14 during ordinary play? With F3 enabled,
watch the SPACE and GRID counters while moving through swell, wreckage, and rock clusters.
Any behavioral difference is a regression rather than an intended tuning change.

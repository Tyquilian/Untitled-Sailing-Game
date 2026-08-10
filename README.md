# Tactical Sailing — Ocean Continuity and Longer Coast (Batch 17)

Batch 17 extends the established ocean to 1350×500 units, adds a western island chain, makes
rocks substantially larger, and allows follow-camera zoom out to 96. Boundary-born swell now
survives the long deep-water crossing and partially obstructed fronts retain their open-water
sections instead of disappearing as a group.

## Run the latest build

Open `Builds/Batch17/TacticalSailingBatch17.exe` and keep the complete `Batch17` directory
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

## Batch 17 changes

- Extends the ocean from 900×500 to 1350×500 units while preserving its vertical scale.
- Adds a deterministic five-island western chain and moves the continent with the east edge.
- Reconstructs 59 ordered phases at the unchanged local period and spacing.
- Reduces accidental deep-water lifetime loss and retains a front until its final section dies.
- Makes breaking and rock absorption partial, timestep-correct energy losses.
- Raises rock radii to `0.80–2.86` units at the reference seed and scales hazards to 784.
- Expands follow-camera scroll from 27 to 96 while retaining full-map view on `M`.
- Keeps bathymetry presentation near four-unit cells as the world length increases.

## Validation summary

Reference editor validation passed on 2026-08-10:

- deterministic reference run: 900/900 matching ticks;
- derived reference/prior/current phases: `19 / 39 / 59` at identical `22.71`-unit spacing;
- normal 30-second population range: `59–64`, ending at 64;
- local/world/reference fronts: `6 / 64 / 7`;
- shelf hazards: `320 → 640 → 784`;
- average expanded crest: `520.15` units with 40-section capacity;
- nominal width crossing: `108.0` seconds;
- boundary-to-shelf continuity: tick `4,801`, energy `0.188`, expiration tick `5,138`;
- deterministic final hash: `C0797F93B819AC6F`;
- 1,000-front stress rate: approximately `55` ticks/second;
- 10,000-front diagnostic: approximately `2.3` ticks/second for 30 ticks;
- packaged frame probe: `8.88ms` average / `12.60ms` p99; and
- all prior wave, vessel, collision, replay, source, target, and object regressions passed.

See `BATCH17_OCEAN_CONTINUITY.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch17 -quit -logFile build-batch17.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, and full-map PNGs beside the executable.

## Playtest question

Do later source-born fronts keep the eastern shore alive as convincingly as the initial sea?
Try the extended scroll range, western island chain, larger rocks, target pursuit, both vessel
profiles, and a long eastward trip. The main questions are shoreline continuity, rock
readability, navigable shelf approaches, and whether the longer crossing feels exploratory.

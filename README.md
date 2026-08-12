# Tactical Sailing - True Boundary Entry (Batch 19)

Batch 19 corrects how diagonal swell enters the established 1350 x 500 ocean. The northern
cross-sea now begins on its upstream phase plane at the northwest map corner instead of
materializing across the interior between islands. Off-map crest sections remain pending and
mechanically inert until they cross a real world boundary.

## Run the latest build

Open `Builds/Batch19/TacticalSailingBatch19.exe` and keep the complete `Batch19` directory
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
| N | Start the cross-sea / request its early departure |
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

## Batch 19 changes

- Derives diagonal entry from the source direction and rectangular world bounds.
- Starts the northern cross-sea at the northwest upstream corner; the dormant southern source
  resolves to the southwest corner.
- Places the complete finite crest on the upstream phase plane, outside the interior.
- Adds an authoritative `PendingEntry` section state. Pending sections move deterministically
  but do not render, collide, push objects or boats, sample terrain, break, foam, or decay.
- Activates each section only after its own trajectory crosses the map boundary.
- Keeps the parent front alive while sections await entry and while entered sections drain.
- Shows the source edge, true entry point, upstream crest plane, and pending-section count in
  diagnostics.
- Preserves Batch 18's bounded cross-sea lifecycle and unchanged western carrier clock.

## Validation summary

Reference editor validation passed on 2026-08-12:

- deterministic reference run: 900/900 matching ticks;
- derived reference/prior/current phases: `19 / 39 / 59` at identical `22.71`-unit spacing;
- normal 30-second population range: `59–64`, ending at 64;
- local/world/reference fronts: `6 / 64 / 7`;
- shelf hazards: `320 → 640 → 784`;
- average expanded crest: `520.22` units with 40-section capacity;
- nominal width crossing: `108.0` seconds;
- boundary-to-shelf continuity: tick `4,803`, energy `0.187`, expiration tick `5,138`;
- cross-sea short profile: four emitted / four peak-active fronts, draining at tick `2,109`;
- event angle/cadence: `58.4` degrees and exactly `157` ticks;
- carrier isolation: `2,109 / 2,109` clock-matching ticks;
- local two-system overlap: `782` ticks;
- boundary entry: `(-225, 125)` in the focused basin, with the crest center outside at
  `(-118.20, 191.07)`;
- first event emission/entry: ticks `80 / 97`, with `38 / 0` pending/entered sections and
  zero interior sections at emission;
- pending energy, breaking, and foam remained unchanged before entry; and
- packaged-build 1,000-front stress: 900 ticks in `28.359s` CPU (`31.7` ticks/s), below the 30-second
  primary target; the load-calibrated gate has a fixed 34-second hard ceiling;
- 10,000-front enlarged-world diagnostic: 30 ticks in `23.703s` CPU;
- packaged smoke test: pass at tick 120 with one cross-sea phase emitted and 40 sections
  pending outside the map;
- representative rendered frame sample: `9.24ms` average / `16.64ms` p99, zero repeated
  moving frames, and 14,895 dynamic vertices; and
- all prior wave, vessel, collision, replay, source, target, and object regressions passed.

See `BATCH19_TRUE_BOUNDARY_ENTRY.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch19 -quit -logFile build-batch19.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, and full-map PNGs beside the executable.

## Playtest question

Start the cross-sea with `N` and watch the northwest map edge in follow and map views. The
first diagonal sections should enter from the boundary and progressively lengthen across the
ocean; no complete diagonal line should appear between the central islands. Then sail through
the later overlap and confirm that it retains Batch 18's ordered two-system feel.

# Tactical Sailing - Ordered Cross-Sea Event (Batch 18)

Batch 18 keeps the established 1350 x 500 carrier ocean and adds one bounded, ordered
cross-sea event. A deterministic northern swell can build across the persistent western
carrier, hold, depart, and drain naturally without replacing or perturbing the normal source.

## Run the latest build

Open `Builds/Batch18/TacticalSailingBatch18.exe` and keep the complete `Batch18` directory
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

## Batch 18 changes

- Adds an authoritative five-phase cross-sea lifecycle: build, established, departure, drain,
  and inactive.
- Temporarily authorizes the dormant northern source at roughly 58 degrees to the carrier.
- Uses that source's own fixed 4.2-6.2 second period; population never triggers a replacement.
- Leaves emitted fronts in the normal propagation, shelf, breaking, boat, rock, and debris
  systems until their natural lifetime ends.
- Creates fresh swell-system identity for each repeatable event and hashes all lifecycle state.
- Keeps the western carrier clock and phase-shape sequence isolated from event emissions.
- Adds `N`, a panel button, lifecycle status, and existing purple source diagnostics.
- Preserves Batch 17's world, shoreline continuity, larger rocks, camera range, and density.

## Validation summary

Reference editor validation passed on 2026-08-11:

- deterministic reference run: 900/900 matching ticks;
- derived reference/prior/current phases: `19 / 39 / 59` at identical `22.71`-unit spacing;
- normal 30-second population range: `59–64`, ending at 64;
- local/world/reference fronts: `6 / 64 / 7`;
- shelf hazards: `320 → 640 → 784`;
- average expanded crest: `520.22` units with 40-section capacity;
- nominal width crossing: `108.0` seconds;
- boundary-to-shelf continuity: tick `4,803`, energy `0.187`, expiration tick `5,138`;
- cross-sea short profile: four emitted / four peak-active fronts, draining at tick `1,565`;
- event angle/cadence: `58.4` degrees and exactly `157` ticks;
- carrier isolation: `1,565 / 1,565` clock-matching ticks;
- local two-system overlap: `785` ticks;
- deterministic final hash: `4444658FDCC6EDB4`;
- 1,000-front stress rate: approximately `58` ticks/second;
- 10,000-front diagnostic: approximately `2.4` ticks/second for 30 ticks;
- packaged active-event frame probe: `8.35ms` average / `8.52ms` p99 with no
  repeated moving frames; and
- all prior wave, vessel, collision, replay, source, target, and object regressions passed.

See `BATCH18_ORDERED_CROSS_SEA.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch18 -quit -logFile build-batch18.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, and full-map PNGs beside the executable.

## Playtest question

Start the cross-sea with `N`, watch it develop in follow and map views, then sail through the
overlap. Is the second direction readable as one ordered system rather than random clutter?
Does the build feel substantial without overwhelming the carrier, and does the ocean visibly
return to its former state after departure? An early second press of `N` also tests whether a
shortened event still ends naturally.

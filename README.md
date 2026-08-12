# Tactical Sailing - Large-Vessel Foundation (Batch 20)

Batch 20 adds a merchant-scale ship to the established 1350 x 500 ocean without adding naval
gameplay. Its 16.5 x 5.2 hull uses thirteen representative samples for waves, land, rocks,
cargo, and wreckage while retaining the same immediate sailing controls and one force
contribution per crest identity.

## Run the latest build

Open `Builds/Batch20/TacticalSailingBatch20.exe` and keep the complete `Batch20` directory
together. Earlier batch builds remain preserved.

The source project targets Unity `6000.3.2f1`. Open
`Assets/WavePrototype/WaveDemo.unity` and enter Play Mode.

## Controls

| Input | Action |
|---|---|
| W / Up | Forward power |
| S / Down | Brake and low-speed reverse |
| A D / Left Right | Steer |
| Y | Cycle the player through skiff, cutter, and merchant (debug) |
| N | Start the cross-sea / request its early departure |
| B | Spawn an arcade skiff at the cursor |
| Shift + B | Spawn a heavy cutter at the cursor |
| Ctrl + B | Spawn a merchant ship at the cursor |
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

## Batch 20 changes

- Adds the immutable `MerchantShip` profile: mass 96, hull 16.5 x 5.2, thirteen samples.
- Extends sampled hull ownership to swept rock collision and cargo/wreckage contact.
- Keeps one wave force, yaw, damage result, and event per crest/ship even when many hull
  samples overlap the same front.
- Gives the merchant slower acceleration, turn, surf, yaw, and damage response while keeping
  the same arcade controls.
- Adds a merchant-specific multi-triangle hull, two sail planes, sampled-footprint highlight,
  and automatic follow-camera framing.
- Exercises the largest hull in the spatial broadphase/brute-force determinism comparison.
- Preserves Batch 19 boundary entry, the western carrier, map, bathymetry, rocks, and density.

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
- pending energy, breaking, and foam remained unchanged before entry;
- merchant reference: mass `96`, hull `16.5 x 5.2`, thirteen samples;
- merchant 90-tick speed / 30-tick turn: `6.15 / 20.9 degrees`;
- merchant crest contacts: one bow-only and one center hit, each exactly one event;
- merchant land/rock/cargo footprint checks: `1 / 1 / 1`, while the same skiff center did not
  collect the bow-positioned cargo;
- merchant breaker damage/displacement: `2.395 / 0.99`, versus cutter `4.010 / 1.77`;
- merchant spatial/brute comparison: 480/480 matching ticks;
- packaged-build 1,000-front stress: 900 ticks in `28.938s` CPU (`31.1` ticks/s), below the
  30-second target; the calibrated floor/hard ceiling are `32 / 34` seconds;
- packaged merchant smoke test: pass at tick 120 with one cross-sea phase and 40 pending
  boundary sections;
- rendered merchant sample: `8.80ms` average / `13.04ms` p99, zero repeated moving frames,
  and 15,156 dynamic vertices; and
- all prior wave, vessel, collision, replay, source, target, and object regressions passed.

See `BATCH20_LARGE_VESSEL_FOUNDATION.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch20 -quit -logFile build-batch20.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, merchant, and full-map PNGs beside the
executable.

## Playtest question

Press `Y` twice to take control of the merchant. Compare its acceleration and turning with the
skiff, then navigate close to islands, rock clusters, floating objects, and crossing swell.
Does the longer footprint and inertia feel like a genuinely larger vessel while remaining
responsive enough for this arcade sea, or should its scale/handling move in either direction?

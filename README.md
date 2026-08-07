# Tactical Sailing — Long-Period Swell (Batch 13)

For the frozen Batch 13 architecture, API, data-model, tuning, validation, and extension
manual, start with [`Documentation/DeveloperManual/README.md`](Documentation/DeveloperManual/README.md).

Batch 13 doubles the unified swell period and gives each map-spanning crest finer local
resolution. The result is a calmer, more legible ocean with fewer phase lines and slightly
more detailed shelf, island-shadow, and breaking deformation along each line.

## Run the latest build

Open `Builds/Batch13/TacticalSailingBatch13.exe` and keep the complete `Batch13` directory
together. Batches 1–12 remain preserved.

The source project targets Unity `6000.3.2f1`. Open
`Assets/WavePrototype/WaveDemo.unity` and enter Play Mode.

## Controls

| Input | Action |
|---|---|
| W / Up | Forward power |
| S / Down | Brake and low-speed reverse |
| A D / Left Right | Steer |
| M | Full map / return to follow camera |
| Mouse wheel | Follow-camera zoom |
| Q | Spawn one natural-format segmented swell front at the cursor |
| Shift + Q | Spawn the seven-packet local breaker burst for comparison |
| T | Relocate the optional target |
| V | Enable or hide the target |
| K | Toggle the short target-bearing arrow |
| Left bracket / Right bracket | Adjust target visit radius |
| B | Spawn a passive test boat at the cursor |
| C | Spawn collectible cargo at the cursor |
| X | Spawn collidable wreckage at the cursor |
| P / Period | Pause / advance one simulation tick |
| F3 | Swell, segment, source, foam, object, collision, and frame diagnostics |
| R | Reset with the same deterministic seed |
| H / F1 | Toggle help |
| Escape | Quit |

## Batch 13 changes

- The western source period doubles from `1.15–1.35` seconds to `2.3–2.7` seconds.
- The reference seed selects a 76-tick period, approximately `2.53` seconds at 30 Hz.
- The reconstructed playable sea starts with 20 unique ordered phases rather than trying
  to fit the former 40 fronts into the wider spacing.
- The local density reference falls from 14 to 7. It remains diagnostic, not enforced.
- Crest target spacing falls from 16 to 13.5 units. A typical playable crest now uses
  20 authoritative sections instead of roughly 18.
- The section ceiling becomes 20. This supplies the requested playable-map detail while
  keeping the honest 1,000-front architecture profile above 30 simulation ticks/second.
- All Batch 12 cadence, partial breaking, residual swell, foam, cursor-front, and local-burst
  behavior remains intact.

## Validation summary

Batch 13 editor verification on the reference development PC (2026-08-06) passed:

- deterministic simulation: 900/900 matching ticks, hash `D7F7C3547E43475B`;
- phase cadence: first observed emission `39/39`, invariant `76/76`-tick period,
  maximum same-tick burst `1` after population was deliberately reduced to zero;
- normal reference population after one explicit injected front: `15–23` fronts;
- average crest length: `266.37` units, with 20 sections per fresh natural front;
- reference crest sections after 900 ticks: `211/300` active;
- partial breaker: energy `2.00 → 1.45 → 1.40`, peak foam `0.260`, and successful
  return to traveling state;
- ordinary passage: `1.07` displacement and a `29.93`-unit final wave lead;
- traveling/breaking displacement `0.10/6.56` and yaw `0.6°/46.3°`;
- island shadow: center section removed, four of five active, `35.26`-unit gap; and
- shelf deformation: all five sections active with `23.30` units of deformation;
- packaged smoke: 120 ticks, 21 fronts, `359/420` active sections, hash
  `18D4F55F3B3FFCE5`; and
- 600-frame graphical run: `2.64 ms` average, `6.28 ms` p99, zero Gen-0
  collections, and no repeated moving frames.

## Scale results

The 320- and 1,000-front profiles use proportionally longer oceans so their doubled-period
fronts occupy distinct ordered phases:

- 20-front playable profile, 900 ticks: `0.563 s` CPU, `1600.0` ticks/second;
- 320-front secondary profile: `9.250 s` CPU, `97.3` ticks/second;
- 1,000-front architecture profile: `29.188 s` CPU, `30.8` ticks/second,
  `891` final fronts; and
- 10,000-front, 1,800×1,000 diagnostic: `13.547 s` CPU for 30 ticks,
  `2.2` ticks/second.

The 1,000-front workload narrowly remains above the 30 Hz fixed-tick target. A real-time
10,000-front ocean still requires spatial or multi-rate scheduling.

See `BATCH13_LONG_PERIOD_SWELL.md` for model details. Batch 12 remains the cadence and
partial-breaking foundation.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch13 -quit -logFile build-batch13.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.

## Next playtest question

Does the approximately 2.5-second rhythm leave enough open water between consequential
fronts without making them trivial to avoid? Also inspect breaking around islands and the
continental shelf: the extra sections should make curved gaps and local deformation read
more smoothly without making one crest contribute multiple boat forces.

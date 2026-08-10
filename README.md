# Tactical Sailing — Exploration-Scale Ocean (Batch 16)

Batch 16 doubles both world dimensions, producing a 900×500 ocean with four times the former
area. Initial swell phases are derived from travel span and the resolved period, shelves are
physically broader, and absolute hazard/object counts grow without preserving cramped-map
density. Sailing and impact tuning remain unchanged.

## Run the latest build

Open `Builds/Batch16/TacticalSailingBatch16.exe` and keep the complete `Batch16` directory
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

## Batch 16 changes

- Expands the playable ocean from 450×250 to 900×500 units and doubles nominal crossing time.
- Derives the normal initial phase count from source-to-exit span divided by packet spacing.
- Preserves zero as the disabled-ocean setting and positive counts as explicit test overrides.
- Raises the crest-section ceiling from 20 to 40 to preserve physical sampling resolution.
- Stretches the established continental/insular shelves into more recognizable regions.
- Scales shelf-driven rocks from 320 to 640 and floating cargo/wreckage from 24 to 48.
- Scales initial boat positions with the map and exposes the derived initial count in the HUD.

## Validation summary

Reference editor validation passed on 2026-08-10:

- deterministic reference run: 900/900 matching ticks;
- derived reference/expanded phases: `19 / 39` at identical `22.71`-unit spacing;
- normal 30-second population range: `37–42`, ending at 38;
- local/world/reference fronts: `4 / 38 / 7`;
- shelf hazards: `320 → 640`;
- average expanded crest: `519.17` units with 40-section capacity;
- nominal width crossing: `72.0` seconds;
- deterministic final hash: `A8991F06A66C842A`;
- 1,000-front stress rate: approximately `59` ticks/second;
- 10,000-front diagnostic: approximately `2.2–2.5` ticks/second for 30 ticks;
- packaged frame probe: `8.71ms` average / `13.53ms` p99; and
- all prior wave, vessel, collision, replay, source, target, and object regressions passed.

See `BATCH16_EXPLORATION_SCALE.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch16 -quit -logFile build-batch16.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, and full-map PNGs beside the executable.

## Playtest question

Does the larger sea create worthwhile travel and discovery, or merely longer empty intervals?
Try normal follow view, full-map navigation, target pursuit, both vessel profiles, and several
shelf crossings. The main questions are landmark readability, encounter frequency, and
whether broad shelves now give breaking fronts enough physical room.

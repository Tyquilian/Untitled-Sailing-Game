# Tactical Sailing — Vessel Profiles (Batch 14)

Batch 14 adds the first real vessel-scale contrast without adding a player mechanic. The
original arcade skiff remains the default and retains its Batch 13 handling. A debug-selectable
heavy cutter has a larger collision footprint, five hull interaction samples, greater inertia,
slower turning, lower speed limits, and greater breaker/grounding resistance.

## Run the latest build

Open `Builds/Batch14/TacticalSailingBatch14.exe` and keep the complete `Batch14` directory
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

## Batch 14 changes

- Adds immutable `ArcadeSkiff` and `HeavyCutter` startup profiles.
- Preserves the former boat values as the skiff profile and default for every initial boat.
- Applies profile mass, propulsion, turn rate, cruise/surf limits, drag, wave response, yaw,
  damage resistance, dimensions, and collision radius through existing systems.
- Samples the cutter at center, bow, stern, port, and starboard for waves and land while
  preserving one impulse per crest identity.
- Uses vessel collision radius for swept rocks and floating-object contact.
- Scales rendered hulls from profile dimensions and exposes debug sample points.
- Corrects local wave-density diagnostics to count a front when an active crest section is
  visible, even if the parent front center is outside the view.

## Validation summary

Reference editor validation passed on 2026-08-10:

- deterministic reference run: 900/900 matching ticks;
- skiff/heavy mass: `7.2 / 24.0`;
- 90-tick skiff/heavy speed: `10.45 / 7.47`;
- skiff/heavy turn response: `66.6° / 37.7°`;
- broad edge contact: skiff `0`, heavy `1`;
- centered heavy contact: exactly `1` event despite five samples;
- broad grounding: skiff `0`, heavy `1`;
- skiff/heavy breaker damage: `5.882 / 3.193`;
- skiff/heavy breaker displacement: `2.79 / 1.51`;
- 20-front playable benchmark: `2133.3` ticks/second;
- 320-front secondary benchmark: `144.4` ticks/second;
- 1,000-front stress benchmark: `49.4` ticks/second; and
- 10,000-front diagnostic: `3.7` ticks/second for 30 ticks.

See `BATCH14_VESSEL_PROFILES.md` for the focused design record and
`Documentation/CODEX_PROJECT_CONTEXT.md` for the compact working context.

Build from PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' -batchmode -nographics -projectPath . -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch14 -quit -logFile build-batch14.log
```

The packaged player also accepts `-smoketest`, `-frametest`, and `-capturepreview`.
The preview mode writes separate skiff, heavy-cutter, and full-map PNGs beside the executable.

## Playtest question

Does the cutter feel heavier without becoming inert, and do broad waves pass beneath it
naturally until breaking water becomes consequential? Compare both hulls with `Y`, then use
`Q` and `Shift + Q` around the continental and insular shelves.

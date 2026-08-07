# Project and Setup

## Purpose

Tactical Sailing is a deterministic, top-down 2D environmental simulation and arcade
sailing sandbox. The ocean is represented by moving packets of energy rather than water
particles or a fluid surface. Waves propagate through a static bathymetric environment,
respond locally to depth and obstacles, and affect boats and floating objects.

Batch 13 proves the following behaviors:

- fixed-tick deterministic simulation on the same build and platform;
- a clocked, continuous western swell;
- broad map-spanning fronts made from locally authoritative crest sections;
- deep-water propagation, shoaling, refraction, partial breaking, foam, and retirement;
- island shadows and cross-crest shelf deformation;
- forgiving arcade propulsion, steering, surfing, slowdown, yaw, grounding, and damage;
- deterministic rocks, an optional roaming target, cargo, and wreckage;
- control-stream replay and full-state hashing; and
- distinct playable, secondary, 1,000-front, and 10,000-front validation profiles.

This is not a fluid solver. There is no continuous height field, particle water, wave-wave
interference, diffraction, reflection, multiplayer, save format, production UI, audio,
combat, economy, AI, or full weather simulation.

## Required software

- Windows 64-bit for the supplied player and build path.
- Unity Editor `6000.3.2f1`.
- No third-party Unity packages are required.
- Git is optional for running but is now initialized for source history.

The package manifest requests only built-in Unity modules for IMGUI, JSON serialization,
image conversion, screenshots, and UnityWebRequest.

## Repository layout

| Path | Purpose |
|---|---|
| `Assets/WavePrototype/Scripts/Simulation` | Authoritative state, systems, environment interfaces, and deterministic logic |
| `Assets/WavePrototype/Scripts/Presentation` | Unity lifecycle, input, cameras, rendering, HUD, diagnostics, and player test modes |
| `Assets/WavePrototype/Editor` | Validation probes and Windows batch builders |
| `Assets/WavePrototype/WaveDemo.unity` | Minimal scene used for editor play and builds |
| `Assets/WavePrototype/WaveVertexColor.shader` | Unlit vertex-color rendering for generated meshes |
| `Packages` | Unity package manifest and lock file |
| `ProjectSettings` | Unity project configuration |
| `Builds` | Generated standalone players; intentionally ignored by Git |
| `BATCH*.md` | Historical implementation reports |
| `Reference Document.txt` | Original constitution |
| `PROJECT_DIRECTION_ADDENDUM_v1.1.md` | Ratified post-playtest direction |
| `Documentation/DeveloperManual` | Frozen human-facing Batch 13 manual |

Unity-generated `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, solution/project files,
builds, and logs are ignored by Git.

## Running in the Unity Editor

1. Open the repository root as a Unity project using Unity `6000.3.2f1`.
2. Open `Assets/WavePrototype/WaveDemo.unity`.
3. Enter Play Mode.

The scene itself can be empty. `WavePrototypeApp.Bootstrap`, marked with
`RuntimeInitializeOnLoadMethod(AfterSceneLoad)`, creates a persistent `GameObject` and adds
the application component if one does not already exist. `Awake` then creates the
simulation, camera, material, static mesh, and dynamic mesh.

## Running the packaged player

Keep the complete `Builds/Batch13` directory together and run
`TacticalSailingBatch13.exe`. The command-line modes are:

| Argument | Behavior |
|---|---|
| none | Interactive sandbox |
| `-smoketest` | Runs 120 controlled ticks, writes one `[WAVE-SMOKE]` result, exits |
| `-frametest` | Warms for 180 frames, measures 600 frames, writes `[WAVE-FRAME]`, exits |
| `-capturepreview` | Drives automatically, saves follow and map PNG captures, exits |

Unity arguments such as `-batchmode`, `-nographics`, `-logFile`, `-screen-fullscreen 0`,
`-screen-width`, and `-screen-height` may be combined with these modes.

## Command-line validation

From PowerShell at the repository root:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' `
  -batchmode -nographics `
  -projectPath . `
  -executeMethod WavePrototype.Editor.BatchBuild.Validate `
  -quit -logFile validation.log
```

Build Batch 13 with:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.com' `
  -batchmode -nographics `
  -projectPath . `
  -executeMethod WavePrototype.Editor.BatchBuild.BuildBatch13 `
  -quit -logFile build-batch13.log
```

The build method always runs the complete validation suite before building. It recreates
and saves an empty `WaveDemo.unity`, then passes that scene explicitly to Unity's Windows
64-bit build pipeline in strict mode. `ProjectSettings/EditorBuildSettings.asset` does not
need to list the scene.

## Normal startup state

The presentation uses seed `1847`. A default reset creates:

- one player boat at `(-175, -93)`, heading `0°`;
- passive boats at `(-165, 84)`, heading `-12°`, and `(103, 96)`, heading `190°`;
- one enabled western source and two disabled cross-sea definitions;
- one persistent swell system;
- 20 initial fronts, normally 20 sections each;
- 24 floating objects, with two cargo items for each wreckage item where placement succeeds;
- one enabled target in deterministic safe water; and
- a deterministic environment with the Batch 13 continental/insular shelf and up to 320 rocks.

## Troubleshooting

| Symptom | Check |
|---|---|
| Nothing appears | Confirm `WavePrototypeApp` compiled and the runtime initialization method was not stripped |
| Magenta geometry | Confirm `WaveVertexColor.shader` exists; the app falls back to `Sprites/Default` if lookup fails |
| Different hashes | Confirm seed, configuration, Unity build/platform, tick-addressed controls, and manual debug operations match |
| Build exits with code 1 | Search the Unity log for `[WAVE-VALIDATION]` and the first thrown `InvalidOperationException` |
| Target will not relocate | The map may not contain a candidate meeting margin, depth, rock clearance, ring-clearance, and minimum-distance rules |
| Cursor spawn fails | `SpawnSwellFront` requires an enabled source with a swell system |
| Local density reads zero beside a crest | Batch 13 counts parent wave centers, not nearby active sections; this is a known diagnostic defect |
| Performance collapses at 10,000 fronts | Expected; Batch 13 updates all active sections and is not a real-time 10k scheduler |

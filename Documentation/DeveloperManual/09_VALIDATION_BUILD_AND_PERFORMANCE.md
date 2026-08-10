# Validation, Building, and Performance

## Validation entry points

`WavePrototype.Editor.BatchBuild` provides these command-line/editor entry points:

| Method | Purpose |
|---|---|
| `Validate` | Runs the complete current suite through `RunValidation`; logs success or exits batch mode with code 1 |
| `ValidateBatch9Contact` | Focused ordinary-passage and traveling/breaking separation checks |
| `ValidateBatch10Scalability` | Simple 10,000-front 30-tick diagnostic with progress logging |
| `ValidateBatch13Scalability` | Runs and reports the 320- and 1,000-front 900-tick profiles |
| `RunValidation` | Complete assertion-based Batch 13 suite used before every build |

Failures throw `InvalidOperationException` prefixed with `[WAVE-VALIDATION]`. There is no
Unity Test Framework test assembly. The partial `BatchBuild` harness is split across
`BatchBuild.cs` (orchestration), `.Architecture.cs`, `.Probes.cs`, `.ProbeTypes.cs`, and
`.Builds.cs`; command-line entry points remain unchanged.

## Complete validation suite

### Initialization and ownership

The suite checks:

- exact default population, boats, world dimensions, local reference, and source/system count;
- sufficient shelf-driven rocks and a safe enabled target;
- mixed, active, safely placed floating objects;
- explicit source and swell-system identity for every natural front;
- valid section arrays, stable indices, clean breaker state, and correct crest span;
- one enabled western source with two dormant definitions; and
- first source emission scheduled at exactly half the selected period.

### Reference deterministic run

Two simulations use seed 1847. Each receives the same natural-format injected front and 900
ticks of explicit throttle/steering. Hashes must match every tick. The run also requires:

- waves propagate and lose energy or expire;
- the player travels and turns materially;
- breaking, rock, boat-contact, damage, and spent states occur;
- enabled source emission advances while dormant sources remain silent;
- wave population stays within the allowed no-refill lifecycle range;
- wind orientation is correct; and
- generated bathymetry contains sufficient land, shallow water, deep basin, continental
  shelf/slope, and insular shelf samples.

### Behavior probes

| Probe | What it protects |
|---|---|
| `ImpactProbe` | An energetic side wave causes material lateral displacement and heading change |
| `CrestCoverageProbe` | A broad crest reaches a boat well off parent center, misses outside coverage, and contributes exactly one hit across section seams |
| `TravelingPassageProbe` | A normal deep-water crest contacts briefly, overtakes a stationary hull, does not break, and does not carry it like a wall |
| `StateImpactProbe` | Traveling and deliberately breaking encounters remain mechanically distinct |
| `SegmentOcclusionProbe` | A synthetic island removes the central section while outer sections continue and open a meaningful shadow |
| `ShelfDeformationProbe` | A synthetic cross-crest shelf changes section travel without deleting navigable sections |
| `SpeedProbe` | Following swell produces surfing acceleration while head-on swell slows the boat |
| `CruiseProbe` | Sustained propulsion approaches cruise without collisions or exceeding the intended envelope |
| `RockSweepProbe` | High-speed swept collision cannot tunnel, remains deterministic, and permits tangential escape |
| `ReplayProbe` | A recorded tick-addressed control stream reproduces the final hash |
| `TargetProbe` | Initial/manual/automatic placement is safe and deterministic; arrival, disable, and reset semantics work |
| `FloatingObjectProbe` | Cargo collection and wreckage contact produce correct lifecycle/events and deterministic state |
| `BreakingDebrisProbe` | Breaking water affects wreckage substantially more than traveling drift and produces identity-gated events |
| `OffshoreBreakingProbe` | An energetic wave breaks in shallow shelf depth while a deep control does not |
| `SourceCadenceProbe` | Destructive population loss cannot trigger refill bursts; first tick and every interval remain phase locked |
| `BreakingLifecycleProbe` | Breaking dissipates partially into foam and surviving coherent sections resume traveling |

Synthetic `IOceanEnvironment` implementations isolate constant-depth, island-occlusion, and
shelf-deformation behavior from the generated map.

## Batch 13 expected reference results

The final validated Batch 13 snapshot reported:

| Measurement | Result |
|---|---:|
| 900-tick paired determinism hash | `D7F7C3547E43475B` |
| Replay hash | `B1C2E7AC0EF073D3` |
| Reference source period | 76 ticks / approximately 2.53 s |
| First observed runtime emission | tick 39 |
| Maximum same-tick source burst after forced loss | 1 |
| Reference population with one injected front | 15–23 |
| Average crest length | 266.37 |
| Fresh natural sections | 20 |
| Reference active sections after 900 ticks | 211/300 |
| Ordinary passage displacement / peak speed / lead | 1.07 / 0.36 / 29.93 |
| Traveling displacement / yaw | 0.10 / 0.6° |
| Breaking displacement / yaw | 6.56 / 46.3° |
| Partial-breaker energy | 2.00 → 1.45 after 1 s → 1.40 after 4 s |
| Partial-breaker peak foam | 0.260 |
| Island shadow | center removed, 4/5 active, 35.26 gap |
| Shelf deformation | 5/5 active, 23.30 spread |

Hashes are regression values for the same build/platform/configuration, not cross-platform
protocol constants.

## Performance profiles

`RunPerformanceProbe` creates configured worlds, advances 900 ticks, uses process CPU time
for the gate, records wall time separately, tracks minimum/final front count, verifies finite
state, and calculates a final hash. Higher-count profiles use proportionally longer oceans so
ordered phases can occupy distinct positions instead of stacking duplicate fronts.

| Profile | World/initial fronts | Batch 13 CPU result | Rate | Gate/meaning |
|---|---:|---:|---:|---|
| Playable | normal / 20 | 0.563 s / 900 ticks | 1600.0 ticks/s | Must remain below 10 CPU s |
| Secondary | extended / 320 | 9.250 s | 97.3 ticks/s | Must remain below 18 CPU s |
| Architecture stress | extended / 1,000 | 29.188 s | 30.8 ticks/s | Must remain below 30 CPU s; final 891 fronts |
| 10k diagnostic | 1800×1000 / 10,000 | 13.547 s / 30 ticks | 2.2 ticks/s | Diagnostic only, not a real-time gate |

The 1,000-front profile has minimal headroom. The 10,000-front result demonstrates data-model
capacity and deterministic advancement, not playable performance.

The high-density config disables initial floating objects and disables normal/breaking/spent
energy losses for the short 10k workload so mass lifecycle removal cannot make the benchmark
artificially cheap. Bathymetry, section movement, derived state, coherence, interaction,
source maintenance, and hashing remain active.

## Packaged-player checks

### Smoke test

The final packaged Batch 13 result after 120 ticks was:

```text
waves=21
segments=359/420 active/total
systems=1
sources=1/3
objects=23
salvage=1/1
rocks=320
visits=0
hash=18D4F55F3B3FFCE5
```

### Graphical frame test

The reference 600-frame result was 2.64 ms average, 6.28 ms p99, 23.26 ms maximum, zero
generation-zero collections, and no repeated rendered frames while the boat was moving.
These values depend on machine load and graphics environment; they are observational rather
than deterministic.

## Build methods

`BuildBatch3` through `BuildBatch13` are retained as separate editor/automation entry points.
Each currently executes the same current `RunValidation`, recreates the same scene, and builds
the current project into its named historical folder. They do not reconstruct historical
source behavior. `BuildWindows` is a stable alias for `BuildBatch13`.

Every builder:

1. runs validation;
2. calls `EnsureScene` to create and save an empty scene at the canonical path;
3. creates the output directory;
4. calls `BuildPipeline.BuildPlayer` for Windows 64-bit with `StrictMode`; and
5. throws on any non-success result.

Because historical builds are already preserved and ignored by Git, new work should normally
add a new batch method/folder rather than overwriting a previous package.

## When extending validation

Any new authoritative feature should add tests for:

- initial/reset state;
- deterministic paired execution;
- hash coverage of its state/configuration;
- event semantics;
- interaction with existing Apply order;
- replay behavior if controlled by player input;
- performance profile effects; and
- packaged smoke/frame behavior when visible.

Prefer a focused synthetic environment when the generated map would make a behavior probe
fragile or difficult to interpret.

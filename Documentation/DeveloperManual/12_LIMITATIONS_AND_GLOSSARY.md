# Limitations and Glossary

## Known implementation limitations

### Simulation and scale

- Every wave section is advanced every tick. There is no spatial interest, coarse stepping,
  streaming, or sleeping.
- Wave/boat and wave/object interaction scale with entity counts. Boat swept-rock collision
  scans all rocks rather than using the environment grid.
- The 1,000-front benchmark runs only narrowly above the 30 Hz target on the reference PC.
- The 10,000-front diagnostic is not real time.
- Section count is capped at 20. Larger cross-map spans increase spacing rather than local
  resolution unless the cap changes.

### Ocean model

- There is no continuous water height, trough, wave-wave interference, diffraction,
  reflection, current, tide, or conservation-law solver.
- One-dimensional crest chains cannot represent arbitrary two-dimensional surface topology.
- Sections do not subdivide; local features can fall between paths.
- Island shadows are permanent missing sections until the parent retires; waves do not reform
  behind islands.
- Multiple different crests add boat forces with no combined-force ceiling.
- One normal source makes the sea legible but intentionally simple.
- Foam is visual/lifecycle residue, not a force-bearing entity.
- Environment samples are cached for four ticks, so sudden small terrain transitions are
  observed with deterministic delay.

### World and gameplay

- Bathymetry is one analytic layout stretched to world dimensions, not multiple maps or
  streamed geography.
- The target proves arrival/relocation only; it is not a route, score, timer, or mission.
- Cargo and wreckage are diagnostics, not inventory or economy.
- There is no defeat/recovery flow when health reaches zero.
- Passive boats receive waves and objects but have no control or AI.
- Every boat shares one point-scale hull profile and circular rock collision.

### Determinism and replay

- Repeatability is supported only for the same build, platform, initial state, configuration,
  and ordered external operations.
- Floating-point behavior is not guaranteed bitwise across CPU/platform/compiler variants.
- Replay records applied boat controls only. Manual debug actions and configuration mutations
  are absent.
- There is no serialized replay file, snapshot, rollback, authority, networking, or state
  replication.

### API and diagnostics

- `SimulationConfig` is publicly mutable although several values are effectively
  construction-only.
- `WaveData.Segments` leaks a mutable array reference through a read-only wave list.
- `SampleWaveDensity` counts parent wave centers and is misleading for map-spanning crests.
- `TargetWaveCount` also disables scheduled source maintenance and initial floating objects
  when nonpositive; its name does not reveal those couplings.
- Several `WaveSourceData` names and fields are historical (`CalmGapSeconds`, spacing/set
  fields, trains) and do not cleanly express the unified phase-stream model.
- `WaveSourceSystem.Frac` is unused in Batch 13.
- Historical build methods build current source into old folder names; they do not recreate
  historical versions.

### Project organization

- `WavePrototypeApp.cs` combines lifecycle, camera, input, mesh building, HUD, automated
  player modes, and diagnostics.
- `BatchBuild.cs` combines validation, benchmarks, synthetic environments, and eleven build
  entry points.
- The simulation depends on UnityEngine math types despite its lifecycle separation.
- There is no automated documentation generator or standalone non-Unity test runner.

## Deliberately deferred systems

Multiplayer, networking, saving, AI, combat, ports, forts, cargo economy, progression, full
weather, wakes, reflections, audio, production UI, manual sail trim, bracing, forced buoy
courses, and simulation-level roll/pitch are outside the Batch 13 architecture pass.

## Glossary

| Term | Meaning in this project |
|---|---|
| Active section | A crest section still participating in simulation/rendering |
| Amplitude | Derived visual/physical scale from energy and effective depth |
| Apply | Phase that commits temporary decisions to authoritative data |
| Authoritative | Persistent state whose value determines future simulation |
| Bathymetry | Static function/grid giving water depth and therefore land/shelves |
| Breaking intensity | Smoothed normalized severity controlling state, loss, forces, damage, and visuals |
| Coherence | Arcade neighbor coupling that keeps traveling crest sections aligned |
| Command | Tick-addressed external input, currently boat control |
| Crest | One `WaveData` identity spanning laterally through ordered sections |
| Cross-sea | A differently directed swell source; dormant in normal Batch 13 play |
| Decide | Phase that computes temporary proposed state without committing it |
| Derived state | Recomputed value such as amplitude, steepness, force, or effective depth |
| Deterministic | Repeatable under the documented same-build/platform/input constraints |
| Effective depth | Sampled depth clamped so excessively deep seabed stops affecting a packet |
| Energy | Master coherent wave quantity from which amplitude, steepness, and force derive |
| Event | One-tick immutable observation published after Apply |
| Foam energy | Non-coherent residue shed by breaking/contact; visual but not independently force-bearing |
| Front | Synonym for one broad `WaveData` crest identity |
| Held control | Last applied control retained for a boat until replaced |
| Parent wave | Aggregate `WaveData` surrounding its locally authoritative section array |
| Packet length | Longitudinal wave scale controlling speed, steepness, contact, and rendering |
| Phase | One periodic crest position/emission in a persistent source stream |
| Presentation | Non-authoritative rendering, UI, input collection, camera, and cosmetics |
| Replay | Reapplication of recorded tick-addressed boat controls to a reset simulation |
| Section/segment | One local sample and state unit along a broad crest; terms are interchangeable here |
| Shoaling | Shallow-water speed reduction and derived amplitude/force increase |
| Source | Boundary generator with timing, energy, direction, and system ownership |
| Spent | Terminal/low-energy section state |
| State hash | 64-bit exact-bit diagnostic digest of authoritative state/config/events |
| Swell system/stream | Persistent shared direction, scale, energy, period, and counters for fronts from one source |
| Traveling | Coherent non-breaking section state |
| Wave | Moving packet/crest of coherent energy, not a water particle or height-field sample |
| Wave shadow | Protected gap produced when terrain removes local crest sections |

## Snapshot conclusion

Batch 13 is a deterministic environmental prototype with a mature single-source swell model,
not a finished ocean simulation or game framework. Its strongest extension seams are the
coordinator façade, environment interface, source/system abstraction, event stream, and
separate floating-object service. Its most immediate technical constraints are point-scale
boats, unreliable local-density instrumentation, one stretched map, and an all-sections/
every-tick scheduler.

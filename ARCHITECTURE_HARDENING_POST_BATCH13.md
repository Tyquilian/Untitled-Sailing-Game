# Post-Batch 13 Architecture Hardening

This intermediary pass responds to the first external developer review. It deliberately
preserves Batch 13 gameplay, world scale, swell cadence, and rendering while reducing risks
that would become expensive during later map and vessel growth.

## Completed changes

- `WavePrototypeApp` remains the Unity lifecycle coordinator but delegates camera behavior,
  input translation, mesh rendering, snapshot interpolation, and diagnostics to focused
  presentation services.
- The editor harness is a partial class split into validation orchestration, architecture
  boundaries, probe implementations, probe result types, and historical build commands.
- Public entity collections use non-castable read-only wrappers. `WaveData.Segments` is an
  allocation-free read-only value view; the authoritative array stays internal.
- `SimulationConfig` is a mutable startup builder only. `WaveSimulation` clones it and exposes
  an immutable `SimulationConfigSnapshot`, so later caller edits cannot partially reconfigure
  a running simulation.
- Expired waves and their reusable decision slots are removed together. Surviving wave IDs
  therefore retain their existing per-segment decision arrays rather than reallocating after
  every earlier removal.
- Boat-input history is explicitly optional and bounded. The default retains the latest
  65,536 applied commands, while consumed pending storage compacts in batches.
- Important wave-impact, reversing, turning, grounding, and rock-damage multipliers now have
  named configuration fields. Defaults preserve the prior equations.
- Validation directly probes configuration isolation, public-state ownership, bounded history,
  and disabled recording in addition to all existing determinism and gameplay tests.

## Deliberately deferred

Spatial wave/boat and boat/rock broadphases remain the next scale-dependent architecture
change, not part of this behavior-preserving pass. Their query ordering must be deterministic,
and the existing 1,000/10,000-front measurements provide the comparison baseline.

This is not numbered Batch 14. Batch 14 remains the planned vessel-profile and broad-hull
interaction batch unless the roadmap is changed after playtesting.

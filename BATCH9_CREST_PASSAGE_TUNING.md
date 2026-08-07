# Batch 9 Crest Passage Tuning

## Scope

Batch 9 corrects the interaction between one boat and one crest. It does not change world
size, population, crest width, segmentation, shelf geometry, source timing, swell-system
placement, or global overlap. The Batch 8 build remains preserved as a comparison baseline.

## Traveling crest semantics

The Batch 8 interaction ellipse used the same broad longitudinal padding for traveling and
breaking sections. A stationary hull could receive forward force for long enough to
accelerate alongside a non-breaking crest and remain caught near its leading edge.

Batch 9 gives traveling sections a distinct response:

- longitudinal radius is packet length × 0.30 plus 0.85 units;
- base impact multiplier is 0.38;
- yaw multiplier is 0.58; and
- impact fades to zero as hull speed with the wave approaches 72% of local crest speed.

Lateral coverage still comes from the physical segmented crest width. The change therefore
shortens passage through the crest without making a broad wave sidesteppable again.

The relative-speed fade is evaluated from current hull velocity and local segment speed.
It introduces no wave/boat references, contact cache, cooldown, or non-deterministic state.

## Breaking crest semantics

Breaking sections retain the Batch 8 longitudinal ellipse, 2.15 impact multiplier, full
yaw scale, surfing assistance, head-on resistance, and damage calculation. Spent foam uses
a reduced 0.12 multiplier.

No combined-force ceiling is introduced. Intersections between legitimate swell families
remain additive. Their excessive frequency is a world-organization problem reserved for
the analytic swell-field experiment rather than suppressed at the boat.

## Validation

The stationary-passage probe places an ordinary 60-unit crest 12 units behind an idle boat
in deep water. Over 150 ticks it must contact the boat, remain below 22 consecutive contact
ticks, move the hull fewer than 3 units, keep peak boat speed below 4, and finish at least
10 units ahead.

The current result is:

- 16 total/consecutive contact ticks;
- 1.07 units of hull displacement;
- 0.36 peak hull speed; and
- 29.93 units of final crest lead.

A paired side-impact probe compares an ordinary traveling section with a deliberately
breaking section. Traveling impact produces 0.10 units of displacement and 0.6 degrees of
yaw; breaking impact produces 3.34 units and 30.8 degrees. The existing surfing, head-on,
side-impact, broad-coverage, segmentation, island-shadow, shelf-deformation, replay, rock,
target, population, and determinism probes remain mandatory.

Performance profiles now gate on Unity process CPU time and report wall time separately.
This prevents unrelated foreground applications from invalidating the architecture test
while retaining the existing 10-, 18-, and 30-second limits.

The final packaged run completed 900 ticks in 1.344/1.416 seconds CPU/wall at 80 fronts,
7.297/7.598 seconds at 320 fronts, and 27.500/29.061 seconds at 1,000 fronts. A 600-frame
standalone graphical run averaged 2.20 ms with a 6.00 ms p99, 6.99 ms maximum, one Gen-0
collection, and no repeated moving frames while the reference machine was under unrelated
foreground load.

## Tuning controls

The following authoritative values live in `SimulationConfig` and are included in the
state hash:

- `TravelingImpactMultiplier`
- `TravelingLongitudinalScale`
- `TravelingLongitudinalPadding`
- `TravelingCarrySpeedFraction`
- `TravelingYawMultiplier`

Existing breaking controls remain independent.

## Deferred work

- Global swell overlap is not capped or normalized.
- Source systems remain spatially and temporally disordered at the world scale.
- The analytic spectral/swell-family concept remains an experiment for Batch 10.
- No multiplayer, storm, streaming, large-vessel, or analytic-to-segment materialization
  system is implied by this tuning batch.

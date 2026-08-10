# Batch 16 — Exploration-Scale Ocean

## Intent

Batch 16 is the first measured world expansion. The playable ocean grows from 450×250 to
900×500 units: twice the width and height, four times the area, and a nominal 72-second
width crossing at the arcade-skiff cruise speed. Sailing and wave-impact tuning are unchanged.

## Ordered swell reconstruction

The normal ocean no longer stores a fixed playable front count. `TargetWaveCount` now has
three startup meanings:

- negative: derive the initial ordered phases from source-to-exit travel span divided by the
  swell system's resolved packet spacing;
- zero: disable persistent ocean population and its initial floating objects; and
- positive: use an explicit fixed count for isolated validation and stress profiles.

At seed 1847, the former 450×250 reference resolves 19 phases and the expanded world resolves
39. Both use the same 22.71-unit packet spacing and 2.53-second source period. Runtime emission
still follows the source phase clock rather than refilling toward the initial count.

## Crest and environment scale

Full-width crests grow from roughly 267 to 519 units. The maximum section count increases
from 20 to 40 so environmental sampling remains near its former physical spacing rather than
turning each section into a 27-unit slab.

Bathymetry retains the proven normalized continental and insular layout. Doubling both world
axes makes shelves, slopes, islands, and deep-basin passages physically broader without adding
fine seabed noise. Rock generation compensates slope thresholds for that geometric stretch
and scales the shelf-hazard target with the square root of area: 320 rocks at the reference
size and 640 in Batch 16. This adds hazards while leaving the larger sea less crowded overall.

Initial boat positions scale with the world so the player begins in the corresponding
southwestern open-water region. Initial cargo and wreckage increase from 24 to 48—more objects
in absolute terms but half their former density per unit area.

## Validation results

Reference validation on 2026-08-10 reported:

- 900/900 deterministic ticks, final hash `A8991F06A66C842A`;
- derived phases `19 → 39`, with identical 22.71-unit local spacing;
- crest-length scale `1.942×`;
- rocks `320 → 640`;
- normal population range `37–42`, with 38 fronts at the end of the run;
- four nearby fronts against the seven-front local diagnostic reference;
- 1,498 total and 1,064 active crest sections after 900 ticks;
- 72.0-second nominal width crossing;
- 1,000-front stress rate about 59 ticks/second;
- 10,000-front enlarged-world diagnostic about 2.2–2.5 ticks/second for 30 ticks; and
- packaged 600-frame probe: 8.71ms average, 13.53ms p99, zero repeated moving frames,
  131,700 static vertices, and 8,251 dynamic vertices.

An explicit 17-front startup still creates exactly 17 fronts, and zero still disables both
initial waves and initial floating objects. All existing impact, passage, vessel, collision,
replay, target, source-cadence, breaking, spatial-equivalence, and allocation probes passed.

## Scope boundary

This batch does not add streaming, persistence, fast travel, ports, economy, multiplayer,
reduced-rate propagation, or a new player action. Every active section remains fixed-tick and
authoritative. The expansion is deliberately limited to four times the former area so actual
play can determine whether distance feels exploratory or merely empty.

## Next gate

Playtest traversal, full-map readability, target usefulness, shelf encounter frequency, and
whether 48 floating objects are discoverable without becoming clutter. If the larger baseline
works, Batch 17 can use the existing dormant source definitions for one bounded ordered storm
or cross-sea event without changing the normal ocean.

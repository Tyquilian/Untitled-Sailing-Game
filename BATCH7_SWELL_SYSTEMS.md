# Batch 7 Swell Systems and Travel Scale

## Scope

Batch 7 enlarges the ocean from 360×200 to 450×250, a 56.25% area increase. The corrected
normal sea begins with 80 broad continuous fronts. Those fronts belong to authoritative
swell systems rather than being generated as unrelated narrow trains.

No player control, sailing subsystem, scoring rule, or density controller is added.

## Swell-system state

Each `SwellSystemData` records:

- system and source identity;
- canonical direction;
- base energy;
- front period/spacing;
- mean longitudinal and crest dimensions;
- source-specific calm gap;
- initial and currently active front counts; and
- birth tick.

Each natural `WaveData` records its `SwellSystemId`; one record represents a complete
broad crest for current physics. Manual cursor-spawned waves continue to use source and
system ID zero.

## Set emission

The initial 80-front sea is assembled from 4–7-front systems. The reference seed creates
14 initial systems, and all three sources contribute at least one.

Within a system:

- fronts share a tightly bounded direction;
- period stays in the 15–22-unit source set band;
- longitudinal dimensions and 52–approximately-98-unit crest widths vary around system
  means; and
- energy follows a leading/body/trailing envelope, with the strongest fronts toward the
  body of the set.

Sources schedule calm intervals after emitting a set. Expired fronts are no longer
replaced individually. Replenishment waits until a complete set can be emitted, allowing
the normal population to breathe below 80. The smaller corrected set size tightens the
reference population range to 73–81. A global safety floor can release a complete set
before its source cooldown ends if several old systems expire together.

## Force semantics

Swell systems organize fronts; they do not apply additional force. Boat interaction,
damage, surfing, slowdown, and yaw remain per-front behavior. `CrestLength` defines the
physical lateral interaction ellipse, so widening a front increases coverage but does not
stack duplicate force. Grouping therefore does not multiply the Batch 3–6 impact model.

## Presentation

Natural play relies on the position, alignment, spacing, and energy variation of the
actual crests. F3 diagnostics derive a long translucent band from the current members of
each swell system. Individual crests remain the only normal visible and physical
encounter units.

F3 reports source set counts, remaining source cooldown, and the first active system
records. The wave inspector reports both source and swell-system identity.

## Enlarged environment

Bathymetry is evaluated in normalized authoring coordinates. Increasing world extents
stretches the proven continental and insular shelf structure coherently rather than
generating new seabed noise around the previous boundary.

The corrected eastern continental profile uses a long shallow shelf followed by a broad
slope. Shallow terrain samples increased from 556 to 841 in the reference grid, making
the continental approach substantially more prominent both visually and mechanically.

Batch 7 uses:

- a 450×250 world;
- 320 shelf-driven rocks with spatial lookup;
- a 36-unit minimum target relocation distance; and
- a nominal full-width cruise crossing of 36 seconds before acceleration, hazards,
  steering, or wave encounters.

## Determinism and performance

The authoritative hash now includes:

- each wave's swell-system identity;
- next system ID;
- every active swell-system field;
- source system counts, calm ranges, and next-emission ticks; and
- all previously hashed wave, boat, target, input, event, and configuration state.

Active system membership is counted in one front pass using system IDs. The lookup is
not enumerated for authoritative behavior, so dictionary ordering cannot affect results.

The corrected packaged build passed the 80-, 320-, and required 1,000-front profiles.
The 1,000-front run completed 900 ticks in 13.117 seconds while maintaining a 993–996
active range. A 600-frame graphical run averaged 1.27 ms, with a 3.66 ms p99, no Gen-0
collections, and no managed-heap growth.

## Current limits

- Systems organize broad center-sampled crests; individual crest segments do not yet
  sample depth and obstruction independently.
- A system can remain active with only a few surviving fronts late in its life.
- Calm intervals may be shortened by the global population safety floor.
- The visual structure band is derived presentation, not another simulation force.
- Large-vessel hull profiles and broad-hull wave interaction remain future work after
  segmented wavefront coherence.

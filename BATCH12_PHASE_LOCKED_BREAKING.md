# Batch 12 Phase-Locked Breaking

## Purpose

Batch 12 corrects three model inconsistencies exposed after the unified swell was added:

1. catastrophic loss could bypass the source clock and inject up to 64 fronts in one tick;
2. a breaking section rapidly destroyed essentially its entire energy budget; and
3. the cursor tool still created seven short early-batch packets rather than a current
   segmented swell front.

The batch changes those systems without introducing an analytic ocean, a fluid surface, a
new player action, or a population-density controller.

## Source time is authoritative

The initial ocean reconstructs an already-running periodic stream. Front zero begins at
half the system packet spacing, so its implied prior emission occurred half a period before
tick zero. `NextEmissionTick` is now exactly half the selected period. Each successful or
failed phase advances by exactly one complete period from the previous scheduled tick.

Runtime wave count is never consulted when deciding whether a source phase exists. The
former safety-floor loop and population headroom ceiling were removed. Consequently:

- a coastline, island, or artificial test may remove many fronts without creating a burst;
- a manual front does not reset or delay the natural source clock;
- `TargetWaveCount` is an initial-world reconstruction/benchmark parameter; and
- normal population becomes an observable result of period, propagation speed, map length,
  terrain loss, and coherent-front retirement.

The destructive cadence regression begins with 40 fronts and applies extreme ordinary
decay until the population reaches zero. Across 240 ticks it records six source emissions,
one front per emission tick, with an invariant 38-tick interval and no refill burst.

## Partial breaking lifecycle

Each authoritative crest section now stores two additional quantities:

- `BreakingIntensity`: a normalized response to how far steepness or amplitude/depth ratio
  exceeds the relevant breaking threshold; and
- `FoamEnergy`: non-coherent energy shed by breaking or rock contact.

Breaking intensity attacks quickly and recovers more slowly. While it remains above the
release threshold, coherent energy decays between configured minimum and maximum rates.
The dissipated share feeds foam, which has its own decay and no independent force. Once a
section has shed enough energy to fall below its local breaking condition, intensity
recovers and the surviving coherent wave returns to `Traveling`.

This means partial breaking is an actual state transition rather than merely a slower
deletion. A 2.00-energy, five-section front held at depth 4.5 breaks, reaches 1.45 average
energy after one second, stabilizes at 1.40 after four seconds, and resumes traveling with
all five sections active. Its peak per-section foam energy is 0.260.

Land remains terminal: the section stops, loses coherent energy at the spent rate, and does
not remain active solely because foam is visible. Rock absorption remains partial and adds
its lost share to foam. Coherent-front retirement still prevents a map-spanning identity
from surviving as an arbitrarily small collection of scraps.

## Mechanical and visual response

Boat force and damage interpolate between traveling and maximum breaking response using
the section's current breaking intensity. The representative validation remains strongly
arcade-readable: traveling versus breaking displacement is 0.10/6.56 units and heading
change is 0.6°/46.3°.

Presentation draws breaker whites from intensity and a fading wake from `FoamEnergy`.
The F3 inspector reports both quantities. Foam is not sampled as an additional wave and
does not multiply force.

## Cursor tools

`Q` calls `SpawnSwellFront`. It selects the active continuous system and creates exactly one
front with that system's direction, packet scale, map-derived crest scale, segmentation,
source ID, and swell-system ID. It increments system diagnostics without changing the
source's scheduled tick.

`Shift + Q` preserves the former seven short manual packets under the explicit name
“Local Breaker Burst.” Those packets keep source/system zero and exist only for targeted
comparison and contact testing.

## Performance correction

Removing count-driven refills revealed that the old 320/1,000-front benchmark stacked many
duplicate phases into the 450×250 sea. Many duplicates then disappeared together, reducing
the measured workload, while the safety floor injected replacements unrelated to source
time.

Batch 12 gives the 320- and 1,000-front profiles longer deterministic oceans capable of
holding their ordered phase sequences. Deep-water processing now precomputes fixed decay
factors, calculates cruise speed once per front, avoids impossible deep-water rock-grid
queries, and skips unnecessary shallow-speed square roots. The honest 1,000-front run
finishes 900 ticks in 25.984 CPU seconds (34.6 ticks/second) with 883 fronts remaining.

## Deferred

- explicit density targets that scale with map area;
- spatial or multi-rate scheduling for real-time 10,000-front worlds;
- active storms and cross-seas;
- larger vessel profiles;
- analytic generation; and
- ports, inventory, economy, combat, and progression.

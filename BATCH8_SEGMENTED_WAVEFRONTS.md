# Batch 8 Segmented Wavefront Coherence

## Scope

Batch 8 changes how one broad crest interacts with the environment. It does not add more
waves, more player controls, a new objective, advanced sailing, or larger vessels. The
accepted Batch 7 world, source sets, 15–22-unit periods, broad crest scale, 73–81 normal
population range, continental shelf, and arcade boat model remain in place.

## Authoritative crest structure

Every broad natural `WaveData` owns an ordered `WaveSegmentData[]`. Segment count is
derived from crest length using approximately 10-unit spacing, with a hard ceiling of 13.
The current natural range is generally 6–11 sections per crest. A segment stores:

- stable index and active state;
- current and previous position;
- local travel direction and speed;
- local energy;
- cached depth and depth gradient; and
- local traveling, breaking, or spent state.

The parent wave retains aggregate position, direction, energy, speed, and state for source
ownership, swell-system diagnostics, compatibility, and population management. Those
aggregate values are derived from the active sections each fixed tick.

## Local environment response

Each active section moves and decays independently. Its sampled depth controls local wave
speed, shoaling, refraction, steepness, and breaking. Land stops only the section that
reaches it and applies accelerated breaking loss. Rocks trigger local breaking and an
immediate local energy reduction. Sections outside the world or below minimum energy
become inactive.

Environment sampling is staggered deterministically across three ticks. Movement and
energy continue every tick; only the comparatively expensive depth/gradient refresh is
staggered. At the current 30 Hz fixed step, a section travels less than one world unit
between refreshes.

The analytic seabed is cached into a deterministic two-unit bilinear grid when an ocean
environment is created. Presentation, boats, targets, rocks, and waves query the same
cached field. This removes repeated trigonometric and island-profile work without adding
randomness or presentation authority.

## Coherence and separation

Active traveling neighbors share a small amount of direction and longitudinal phase.
This smooths local response into a readable curved front. Coherence applies only while
both neighbors are traveling and within the configured link distance. A breaking,
inactive, or sufficiently separated section does not pull its neighbor, allowing a crest
to split around land and leave protected water behind it.

A crest retires when fewer than 45% of its original sections remain active. This is a
population-quality rule: a few distant scraps cannot continue counting as one of the
roughly 80 normal broad fronts. Source scheduling then replaces the retired crest through
the existing complete-set policy.

## Boat force semantics

For each crest/boat pair, interaction evaluates all live local sections but selects only
the nearest overlapping section. Force, surfing assistance, head-on resistance, yaw, and
breaking damage are applied once from that section. Segment overlap therefore cannot
multiply a single crest into multiple impacts.

The 70-unit coverage regression remains explicit: a boat 33.6 units off center receives
one hit, while a boat 50.4 units off center receives none.

## Presentation

The renderer draws the actual ordered section positions. Neighboring live sections overlap
slightly to form a continuous curved crest. Missing or separated neighbors create visible
gaps. Breaking and foam overlays are local to the affected section rather than spanning
the parent crest.

The HUD reports active/total section counts and local break/foam counts. F3 draws direction
vectors per section, and the cursor inspector selects a section rather than only the parent
wave center.

## Deterministic validation

The authoritative hash includes every segment field and all segment-related configuration.
Batch 8 adds three focused probes:

- a circular island removes the middle section of a seven-section crest while six outer
  sections continue, producing a 35.83-unit shadow;
- a navigable cross-crest shelf keeps all seven sections alive while producing 23.16 units
  of forward deformation; and
- the broad-crest coverage probe confirms one force event at overlapping segment seams.

The 900-tick reference scenario remains repeatable and retains at least 70% of its total
section capacity after lifecycle replacement. The 80-, 320-, and required 1,000-front
profiles remain separate validation gates.

The final packaged validation completed 900 ticks in 1.211 seconds at 80 fronts, 4.629
seconds at 320 fronts, and 15.246 seconds at 1,000 fronts. The 600-frame graphical run
averaged 1.55 ms with a 3.55 ms p99 and 4.35 ms maximum. It observed one 60 KB Gen-0
collection during crest lifecycle allocation; no corresponding frame spike was observed.

## Tuning points

Segment behavior is centralized in `SimulationConfig`:

- `WaveSegmentTargetSpacing`
- `WaveMaximumSegments`
- `WaveEnvironmentSampleInterval`
- `WaveSegmentDirectionCoherence`
- `WaveSegmentPositionCoherence`
- `WaveSegmentLinkBreakMultiplier`
- `WaveMinimumActiveSegmentFraction`

These controls alter simulation state and are included in the deterministic hash.

## Current limits

- Sections form a one-dimensional crest chain; there is no two-dimensional water-surface
  mesh or wave-wave interference solver.
- Local environment sampling is point-based per section. Very small rocks can fall between
  adjacent section paths, though island- and shelf-scale features are resolved well.
- A section disappears as a whole below minimum energy; it does not subdivide further.
- Coherence is an arcade constraint, not a fluid solver, diffraction model, or conservation
  claim.
- Large-vessel hull profiles remain future work. The current boat still selects one local
  section as a point-scale encounter.

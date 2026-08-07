# Batch 13 Long-Period Swell

## Purpose

Batch 13 is a focused rhythm and crest-resolution pass. It does not change the Batch 12
source architecture or breaking lifecycle. It tests a calmer ocean in which each front has
more temporal separation and slightly finer local interaction with terrain.

## Doubled source period

The active western source now selects one deterministic period between 2.3 and 2.7 seconds,
exactly twice the previous 1.15–1.35-second range. Packet spacing remains derived from
deep-water packet speed multiplied by that period, so the temporal and spatial rhythm stay
consistent.

The reference seed selects 76 fixed ticks, or approximately 2.53 seconds. Its first runtime
front is observed after tick 39: half a phase plus the coordinator's post-step tick advance.
Every later observed front is exactly 76 ticks apart.

The destructive cadence probe again removes every existing front. It records three source
emissions over 240 ticks, a maximum same-tick burst of one, and an invariant 76-tick gap.
Longer calm water therefore does not reactivate population-driven replacement.

## Phase-compatible initial sea

At the new spacing, roughly 20 unique phase fronts fit across the 450-unit map. The playable
initial reconstruction is therefore 20 fronts rather than 40. Keeping 40 would require
duplicate longitudinal phases or arbitrary fallback placement, recreating the disorder that
the unified source was designed to remove.

After one explicit cursor-front injection, the 30-second reference run ranges from 15 to 23
fronts. This is an outcome of source period, coherent-front lifetime, island loss, and map
length; it is not count-controlled.

The local diagnostic reference is seven fronts. It is not a spawn condition or distribution
target.

## Finer segmented crests

Natural target spacing falls from 16 to 13.5 units. Across the current approximately
266–270-unit crest, this produces 20 authoritative sections rather than roughly 18.
The maximum is 20.

The added sections improve:

- the shape of island shadows;
- the transition between deep water, outer shelf, and breaking water;
- local rock and land removal; and
- visual continuity where neighboring sections refract at different rates.

They do not create extra front identities. Boat and floating-object systems still select a
single best section from each crest, preserving one contribution per crest/entity encounter.

## Performance boundary

Environment depth and gradient samples move from every third tick to every fourth tick.
At current deep-water speed that is approximately 1.15 units of travel between samples;
the existing shelf deformation, breaking, island shadow, and rock-contact probes continue
to pass.

The longer 320- and 1,000-front benchmark oceans were doubled longitudinally so each front
can occupy a unique phase at the wider packet spacing. Allowing 23 sections on slightly
rotated long-map crests reduced the 1,000-front profile below 30 Hz. A 20-section ceiling
and four-tick environment sampling retain the playable-map improvement with useful
architecture headroom.

- 320 fronts: 9.250 CPU seconds for 900 ticks;
- 1,000 fronts: 29.188 CPU seconds for 900 ticks, or 30.8 ticks/second; and
- 10,000 fronts: 13.547 CPU seconds for 30 ticks.

This leaves little remaining 1,000-front headroom. Further increases in crest resolution,
world scale, or active systems should follow spatial/multi-rate scheduling rather than
raising the all-sections/every-tick ceiling.

## Preserved behavior

- phase-authoritative emissions with no population refill;
- partial severity-dependent breaking;
- coherent energy transfer into non-force-bearing foam;
- residual swell returning to traveling state;
- terminal land interaction and partial rock absorption;
- one natural-format `Q` front; and
- explicit `Shift + Q` local legacy burst.

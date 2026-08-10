# Batch 17 — Ocean Continuity and Coastline Expansion

## Intent

Batch 17 responds to long-session playtesting: pre-seeded fronts reached the eastern coast,
but later boundary-born fronts appeared to expire offshore. It also provides substantially
more follow-camera zoom, a longer east-west exploration span, genuinely new shoreline, and
larger rock hazards.

## Wave lifetime correction

Waves never had an age or wall-clock despawn timer. Two independent rules nevertheless
created an accidental effective lifetime:

- deep-water energy retained only about 28 percent over the former 900-unit transit; and
- a complete parent front expired when fewer than 45 percent of its sections remained.

Deep-water decay is now `0.0025` per second instead of `0.012`. A segmented front remains
authoritative until its final section dissipates or exits, allowing islands and reefs to open
large shadows without deleting unobstructed portions of the same crest.

Breaking loss is reduced from `0.14–0.78` to `0.035–0.24` energy per second. It remains a
severity-dependent partial conversion to foam, but a broad shelf no longer acts as an
automatic deletion corridor. Rock absorption is now timestep-correct: contact applies its
configured per-second rate rather than repeating a fixed energy percentage every tick.

The new continuity probe follows one natural-format, `0.82`-energy front from the western
boundary for up to eight simulation minutes. At seed 1847 it reaches the eastern near shelf
at tick 4,801 with `0.188` energy, continues after the active crest falls below the historical
45-percent cutoff, travels to `x = 559.4`, and expires at tick 5,138 after its terrain
encounter. The probe observed one surviving section out of the original 40 before final
dissipation.

## Longitudinal geography

The world grows from 900×500 to 1350×500 units: six times the Batch 15 area and a nominal
108-second width crossing at skiff cruise speed. Vertical bathymetry scale remains fixed.
Increasing width now reveals additional authoring space instead of stretching every existing
island along X.

The established central archipelagos retain their Batch 16 physical sizes. The continental
margin follows the new eastern boundary, while a five-island western chain occupies the new
water. The normal seed reconstructs 59 ordered phases, up from 39, without changing the
22.71-unit local spacing or roughly 2.53-second source period.

Bathymetry presentation derives its grid dimensions from world size and retains approximately
four-unit cells. The full map therefore gains longitudinal detail instead of rendering the
new coastline with wider tiles.

## Rocks and camera

Deterministic shelf hazards increase from 640 to 784. Generated radii now span `0.80–2.86`
units at seed 1847, averaging `1.44`; Batch 16 ranged down to roughly `0.40`. Thirty-eight
rocks occupy the new western region. Radius-aware separation remains authoritative, so the
larger hazards do not overlap merely to satisfy a count.

Follow-camera scroll range increases from orthographic size 27 to 96 and uses a faster
logarithmic step. `M` remains the complete-map view.

## Validation results

Reference validation on 2026-08-10 reported:

- 900/900 deterministic ticks, final hash `C0797F93B819AC6F`;
- derived phases `19 → 39 → 59` at identical `22.71`-unit spacing;
- normal population range `59–64`, ending at 64 fronts;
- six nearby fronts against the seven-front diagnostic reference;
- 784 rocks with `0.80 / 1.44 / 2.86` minimum/average/maximum radii;
- 1,793 active sections out of 2,528 total after 900 ticks;
- a 108.0-second nominal width crossing;
- 1,000-front stress rate about 55 ticks/second;
- 10,000-front diagnostic about 2.3 ticks/second for 30 ticks; and
- packaged 600-frame probe 8.88ms average and 12.60ms p99, with no repeated moving frames,
  192,520 static vertices, and 15,467 dynamic vertices.

All existing impact, passage, vessel, collision, replay, target, source-cadence, breaking,
spatial-equivalence, allocation, smoke, and build gates passed.

## Scope boundary and next gate

This batch does not add streaming, persistence, ports, economy, multiplayer, weather, or a
new player action. All active crest sections remain authoritative fixed-tick entities.

Playtest sustained shoreline activity, western-chain navigation, rock readability, the
expanded scroll range, and whether the 108-second crossing creates useful travel. A bounded
ordered storm or cross-sea remains the next likely environmental batch after this carrier
ocean is accepted.

# Batch 19 - True Boundary Entry

## Intent

Batch 18 proved that two ordered swell systems can coexist, but its diagonal event front was
centered on the northern source segment. Because the crest already spanned the map, a newly
emitted phase appeared between the islands near the middle of the ocean. The source clock was
correct; the spatial entry geometry was not.

Batch 19 makes directional sources enter from the upstream edge of the simulation domain. It
does not add a sea-state director, weather gameplay, encounters, or level content.

## Directional phase geometry

For a normalized travel direction `d`, each corner of the rectangular world is projected onto
`d`. The minimum projection is the first phase plane that can touch the map. The northern
cross-sea travels southeast, so its minimum is the northwest corner. The dormant southern
cross-sea travels northeast, so its minimum is the southwest corner.

The perpendicular crest axis is projected over the same four corners to find the finite range
of trajectories that can intersect the rectangle. The emission center is reconstructed from
the upstream phase projection and the midpoint of that lateral range. It may—and usually
should—be outside the map. A small lateral inset removes measure-zero corner rays that could
step across a mathematical corner without producing an in-bounds fixed-tick sample.

The western carrier retains its existing boundary-segment entry. This batch changes only
sources marked `DirectionalCorner`; it does not alter normal-ocean phase reconstruction,
spacing, density, or shoreline continuity.

## Pending entry state

An off-map section is authoritative but uses `WaveState.PendingEntry`. Each pending section:

- advances at its system's deep-water cruise speed and fixed-tick direction;
- retains its emitted energy;
- performs no bathymetry or rock queries;
- cannot break, create foam, render, enter a spatial interaction index, push a boat or object,
  contribute to ambient force, or count toward visible density; and
- transitions once, to `Traveling`, when its own next position lies inside the world bounds.

The parent front counts pending sections as alive, preventing premature expiration. Aggregate
front position, direction, energy, and force use only entered sections. After entry, the
section follows the unchanged propagation, refraction, breaking, obstacle, interaction, and
exit rules. There is no re-entry after a section has left the map.

`PendingEntry` reuses the existing one-byte state field instead of adding another boolean to
every authoritative and predicted section in the hot path. Pending sections deliberately set
their ordinary `Active` flag false, so all existing render, density, broadphase, force, and
object paths reject them without another state check. A boundary-aware propagation route is
enabled only after a directional system exists; the normal western ocean retains the exact
ordinary-wave propagation loop used by Batch 18.

## Diagnostics

With `F3` active, directional systems show:

- the declared world-edge source line;
- a highlighted circle at the resolved upstream corner;
- an arrow showing travel direction;
- the complete upstream crest plane; and
- a global pending-section count in the main HUD.

Only entered portions use normal wave rendering. A diagonal crest therefore grows visibly
from the boundary rather than appearing at full width in the interior.

## Focused validation

The deterministic 450 x 250 constant-depth probe reported:

- northern entry point `(-225, 125)` and upstream emission center `(-118.20, 191.07)`;
- first phase emission at tick 80 and first section entry at tick 97;
- 38 pending, zero entered, and zero interior sections on that first emission tick;
- 107 pending sections at peak while multiple event phases approached the map;
- unchanged pending energy with zero breaking and zero foam;
- exact 157-tick event cadence with no burst emission;
- 58.4-degree separation from the western carrier;
- 782 ticks of central carrier/event overlap;
- complete natural drain at tick 2,109;
- carrier source-clock agreement on every comparison tick; and
- fresh swell-system identity on a repeat event.

A separate southern-source check resolves the southwest upstream corner and confirms that its
first emitted phase is likewise entirely pending rather than materialized in the interior.

## Packaged verification

The Batch 19 Windows player passed its headless smoke test at tick 120 with one cross-sea
phase emitted and 40 sections still pending outside the world. The inspected early-event map
preview shows only a short diagonal crest entering at the extreme northwest corner, with no
event crest between the central islands. A repeat 1,600 x 900 frame probe reported 9.24 ms
average, 16.64 ms p99, zero repeated moving frames, and 14,895 dynamic vertices.

## Roadmap boundary

The former sea-state-director proposal is deferred. Choosing when storms occur and what they
mean to the player is game orchestration, not required simulation infrastructure.

The next intended batch remains the large-vessel foundation. The following batch should be a
Unity world-authoring foundation—assets, gizmos, scene handles, deterministic previews, and
validation for developers—not a batch of authored encounters or level design. New regions and
environmental encounters should wait until that editor workflow exists.

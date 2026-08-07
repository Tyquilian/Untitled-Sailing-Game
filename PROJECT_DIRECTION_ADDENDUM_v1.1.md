# Wave System Prototype — Project Direction Addendum v1.1

Date: 2026-07-20  
Status: Ratified direction for Batch 4 and later prototype work

This addendum records playtest-driven decisions made after Batches 1–3. It supplements, but does not replace or rewrite, `Reference Document.txt`. The original document remains the architectural and historical baseline.

## Proven prototype hypothesis

The environment is the central gameplay system. Waves, bathymetry, islands, reefs, and rocks should create readable navigation decisions and unexpected outcomes without requiring many player actions. Batch 3 demonstrated this through meaningful wave displacement, heading changes, surfing, grounding, sheltered water, and hazardous rock clusters.

## Ratified direction changes

- The prototype may be a free-roaming arcade sailing sandbox rather than only a technical visualization.
- Sailing must stay immediate and forgiving. Advanced sail trim and simulation-heavy sailing are not current goals.
- For the current 450×250 map, Batch 13 normal gameplay seeds 20 map-spanning fronts from
  one unified 2.3–2.7-second swell stream. Observed population remains an outcome of period,
  propagation, terrain loss, and coherent-front lifetime. Seven fronts in the local view is
  a diagnostic reference rather than an enforced distribution. The earlier 40-front
  short-period profile and 80-world/30-local short-crest targets remain historical
  comparisons.
- A 1,000-wave scenario remains a required architecture and performance benchmark, not the preferred visual density. A future batch may replace fixed world and visible counts with an explicit target-density model that scales with map area and camera coverage.
- Deterministic analytic bathymetry and deterministic height-dependent rock generation are allowed. Bathymetry should favor broad continental and insular shelves around a mostly uniform deep basin, not detailed or noisy seabed simulation. Depth beyond a wave packet's useful influence should not affect its surface behavior.
- Large maps, clustered navigation hazards, strong wave-to-boat displacement, yaw, slowdown, and temporary surfing are core prototype behavior.
- Ocean size should increase gradually when each expansion supports exploration and meaningful travel. A future open-world sailing direction is a valid product gate, but does not yet authorize streaming, persistence, progression, or other open-world subsystems.
- Broad continuous wavefronts may belong to authoritative swell systems that establish shared direction, period, scale, energy envelopes, source timing, and calm intervals without multiplying per-front force. Longitudinal trains of narrow, easily sidestepped packets are not the desired overarching wave structure.
- Broad crests may be segmented into connected authoritative sections so depth, refraction, rocks, land, breaking, and protected-water gaps can vary locally along one front. Segmentation must preserve one force contribution per crest/boat encounter and must not become a fluid-surface simulation.
- Larger and heavier player vessels are an intended future capability. Vessel profiles and broad-hull wave interaction should follow segmented wavefront coherence rather than treating large ships as point-sized versions of the current boat.
- One optional roaming target marker with an adjustable visit radius and simple visit counter is allowed as lightweight exploration structure. It is not a buoy course, checkpoint chain, timer, score, physical obstacle, or forced objective.
- Propulsion cruise speed and wave-enabled surf speed are separate concepts. Waves may temporarily push a boat beyond its propulsion cruise speed.
- Non-breaking swell should pass beneath and overtake a stationary hull rather than behave as a persistent moving force wall. Strong sustained displacement, yaw, surfing, and damage should be concentrated in energetic breaking encounters.
- Visual presentation may interpolate simulation snapshots, but it must never feed interpolated values back into simulation state.
- Persistent boundary swell streams may continuously emit discrete segmented fronts. Initial
  world state may represent an already-running stream rather than a collection of local
  one-shot wave sets.
- Lightweight floating objects are allowed as a sandbox-engine probe. Cargo collection and
  wreckage contact remain simple diagnostics, not commitments to inventory, economy,
  progression, ports, or combat.
- Breaking may use a depth-limited amplitude/depth criterion in addition to steepness so
  energetic fronts can break on the recognizable outer shelf rather than only at rocks and land.
- An optional short target-bearing arrow is allowed as presentation/debug guidance. It does
  not change the target into a course or physical buoy.
- Normal ocean generation may use one active windward source with fronts spanning the map.
  Cross-sea source definitions may remain dormant for later storms or scenarios but must not
  emit during normal play.
- A breaking crest may deliver one identity-gated impulse to floating cargo or wreckage.
  This strengthens environmental consequence without adding a player action.
- A source's phase clock is authoritative. Initial front count reconstructs an already-running
  sea but does not permit population loss to spawn unscheduled replacements or suppress a
  scheduled phase.
- Breaking should dissipate a severity-dependent portion of coherent energy into non-coherent
  foam. A shelf-limited segment may stabilize and continue as residual traveling swell;
  land may still consume it completely. Foam is not an additional force-bearing wave.
- Manual inspection tools should either construct current natural-format fronts or identify
  themselves explicitly as local legacy/debug packets.

## Rejected or deferred additions

- No bracing mechanic.
- No buoy course, checkpoint chain, or forced multi-region course structure. The single optional roaming target is the narrow exception described above.
- No manual sail-trim subsystem.
- No additional player action merely to increase mechanical complexity.
- Boat lean, pitch, hull-shadow motion, spray, and camera shake remain optional presentation effects only. They are not simulation variables.
- Inventory, cargo economy, progression, ports, forts, combat, AI, full weather simulation,
  multiplayer, networking, saving, audio, wakes, reflections, and production UI remain
  outside the current architecture pass.

## Current engineering priorities

1. Preserve Batch 3's successful environmental feel with a 20-front long-period initial
   reconstruction and phase-authoritative map-spanning swell in the 450×250 playable world.
   Evaluate the resulting no-refill density by playtesting rather than silently enforcing
   a count.
2. Use broad continental and insular shelves to create land, shoaling water, and clustered navigation hazards while leaving deep water mechanically quiet.
3. Keep the roaming target optional, deterministic, locally safe, and limited to a visit counter plus debug controls.
4. Maintain continuous deterministic cadence through one normal-ocean swell stream. Keep
   cross-sea definitions dormant until a storm or scenario explicitly owns them.
5. Keep authoritative state privately owned behind the deterministic coordinator and retain tick-addressed boat-control replay.
6. Maintain separate 20-front playable, 320-front secondary, and 1,000-front
   architecture/performance profiles. Higher-count profiles should use oceans long enough
   to hold distinct ordered phases rather than duplicate phases plus population refills.
   Use a 10,000-front enlarged-world diagnostic to expose scheduling limits; it is not a
   real-time gate.
7. Stabilize segmented wavefront coherence and individual crest passage through playtesting before introducing larger vessel profiles.
8. Keep analytic deep-water generation on hold while persistent discrete streams are
   evaluated. Analytic generation may later feed the source boundary without replacing
   segmented local authority.
9. Return to player-facing environmental development; further architecture work must be justified by a concrete limitation. The 10,000-front result justifies a future spatial interest/multi-rate scheduling experiment if world size continues to grow.

## Architectural principles retained from v1.0

- Simulation owns authoritative state; presentation only observes it.
- The simulation advances on a deterministic fixed timestep independent of rendered frame rate.
- Energy remains the master wave quantity; amplitude, steepness, and force remain derived.
- Environment queries affect gameplay and are not cosmetic decoration.
- Boats and waves do not retain direct references to one another.
- New mechanics must earn their complexity by strengthening environmental decision-making.

## Determinism claim

The current supported claim is repeatability for the same build, platform, initial state, fixed-tick input sequence, and simulation configuration. Cross-platform bitwise determinism, rollback networking, and multiplayer-grade replay are not yet claimed.

## Next product decision gate

After stabilization, architecture consolidation, and ocean-coherence work, choose deliberately between:

1. A deeper environmental sandbox with more maps, sources, and inspection tools; or
2. A minimally structured game using cargo, damage consequences, and landmark navigation.

Neither path is implied by this addendum, and neither should begin by adding extra player controls.

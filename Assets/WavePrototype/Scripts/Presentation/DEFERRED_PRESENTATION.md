# Deferred boat-impact presentation

Batch 3 intentionally does **not** implement simulated boat roll, pitch, animated hull-shadow movement, spray, or camera shake.

If later visual testing shows that actual displacement, yaw, slowdown, surfing, and breaking are insufficient to communicate impact, these effects may be reconsidered under the following boundary:

- They live exclusively in `Presentation`.
- They consume derived local wave force or simulation events such as `WaveHitBoat`.
- They do not become persistent `BoatData` or `WaveData` fields.
- They do not alter forces, damage, collision, steering, energy, or deterministic hashes.
- The top-down silhouette must remain readable; sprite distortion is not acceptable merely for spectacle.

Simple 2D contact foam would be the least intrusive candidate. Roll/pitch approximations and animated shadows require a separate visual-direction review before implementation.

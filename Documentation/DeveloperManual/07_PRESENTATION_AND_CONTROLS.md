# Presentation and Controls

## Application bootstrap

`WavePrototypeApp` is a `MonoBehaviour` with execution order `-1000`. After every scene load,
`Bootstrap` creates a persistent `Wave Prototype` object if no instance exists. `Awake`:

1. Requests a 120 FPS render target.
2. Creates `WaveSimulation` with seed 1847.
3. Creates an orthographic camera.
4. Creates one static and one dynamic mesh using a vertex-color material.
5. Initializes render snapshots and geometry.

The simulation itself remains fixed at 30 ticks per second.

## Update and camera behavior

`Update` reads keyboard input, queues the current player control, advances fixed simulation
ticks through an accumulator, changes follow-camera zoom, rebuilds dynamic geometry, and
records frame time. At most six ticks are executed in one render frame.

`LateUpdate` moves the camera after simulation/render state is prepared:

- Map mode targets world origin and a size that fits both world dimensions plus four units.
- Follow mode targets the interpolated player plus smoothed velocity look-ahead capped at
  nine units.
- Target position is clamped so the viewport remains within world extents.
- Position uses `SmoothDamp`; zoom uses exponential interpolation.

Follow zoom is clamped to 10.5–27 orthographic units. Map view ignores the follow zoom.

## Render interpolation

The presentation keeps previous/current dictionaries keyed by stable wave and boat IDs.
Before a simulation step it swaps dictionaries; afterward it captures current authoritative
state. Render alpha is `accumulator / fixedDeltaTime`, or one while paused.

For waves, parent position, direction, energy, speed, packet length, and crest length are
interpolated. Segment rendering independently interpolates `PreviousPosition` to `Position`
from authoritative segment data. Boats interpolate position, velocity, heading, and health.

External debug mutation—spawning a front or boat—captures current state and supplies a
matching previous entry for new identities to prevent a visual jump. Target/object-only
operations invalidate the diagnostic hash but do not use wave/boat snapshot refresh.

Interpolated values are temporary copies passed to mesh generation. They are never assigned
to `WaveSimulation`.

## Mesh rendering

### Static mesh

`RebuildStaticMesh` creates:

- a 225×125 bathymetry grid covering the full world;
- a dark outer rock circle; and
- a smaller lighter rock center.

Depth colors distinguish land, nearshore, shelf, slope, and deep water. Static mesh bounds
are set manually to avoid an expensive bounds recalculation and incorrect culling.

### Dynamic mesh

`BuildDynamicMesh` recreates vertex/color/index lists every rendered frame for:

- optional diagnostic swell-system bands and source boundaries;
- all active wave sections;
- the target marker and optional bearing arrow;
- cargo and wreckage;
- optional rock interaction radii;
- player highlight and all boats; and
- optional ambient-wave and wind vectors.

The mesh uses 32-bit indices and `MarkDynamic`. Geometry helpers build triangles, quads,
vectors, arrows, circles, and rings directly into shared lists.

### Wave appearance

Each active section calculates a visual tangent from linked active neighbors. Gaps or links
beyond the coherence-distance threshold are not bridged. Visual span covers half the distance
to each linked neighbor, with shorter free ends for internal breaks.

Thickness is packet length ×0.22 plus amplitude ×0.16, clamped to 0.58–1.85. Traveling color
varies from blue to cyan with energy; debug mode varies blue to red. Breaking sections add
two white/foam strips behind the crest. Traveling shallow sections add a subtle shoaling
trace. Residual foam can remain after a section returns to traveling.

All of this is presentation-only; colors and geometry do not alter state.

## HUD and diagnostics

The IMGUI HUD reports player speed, cruise/surf caps, hull, depth, wind drive, ambient field,
position, target, object/salvage totals, front/system/segment counts, breaking/foam sections,
sources, rocks, seed, tick, and a throttled state hash.

The hash is recomputed at most every 0.25 unscaled seconds unless explicitly invalidated.
Frame diagnostics use 240-frame windows and report average and maximum render-frame time.

F3 additionally shows:

- source boundaries/directions and enabled state;
- translucent system structure bands;
- section direction/energy vectors;
- rock interaction radii;
- ambient wave and wind vectors;
- per-source next phase and emitted counts;
- the first five swell systems; and
- a cursor-selected crest-section inspector.

The inspector displays wave/segment identity, state, source/system, energy, derived amplitude,
breaking intensity, foam, speed, depth, and steepness.

The HUD's `LOCAL` figure comes from the known parent-center density diagnostic and should not
be treated as a correct count of visible map-spanning crests.

## Controls

| Input | Action | Simulation effect |
|---|---|---|
| W / Up | Forward throttle | Queues throttle `1` |
| S / Down | Brake/reverse | Queues throttle `-0.35` |
| A / Left | Steer left | Queues steering `+1` |
| D / Right | Steer right | Queues steering `-1` |
| Mouse wheel | Follow zoom | Presentation only |
| M | Full map/follow | Presentation only |
| Q | Spawn full swell front at cursor | One natural system-attributed front at energy 2.65; source phase clock unchanged |
| Shift+Q | Local breaker burst | Seven source-zero manual packets across the wind-perpendicular axis |
| B | Spawn passive boat | Adds same Batch 13 boat profile; it retains zero held controls |
| C | Spawn cargo | Requires safe clear water |
| X | Spawn wreckage | Requires safe clear water |
| T | Relocate target | Deterministic manual relocation |
| V | Toggle target | Changes authoritative target enabled state |
| K | Toggle bearing arrow | Presentation only |
| [ / ] | Target radius -/+ 1 | Authoritative radius, clamped 2–15 |
| P | Pause/resume | Presentation scheduling only; snapshots collapse on transition |
| Period | Pause and advance one tick | Calls one authoritative step |
| F3 | Diagnostics | Presentation only |
| R | Reset same selected seed | Full authoritative reset |
| HUD Seed -/+ | Change seed and reset | Full authoritative reset |
| H / F1 | Toggle help | Presentation only |
| Escape | Quit | Application lifecycle |

The HUD buttons expose the same operations plus reset-visits and map controls.

## Automated player modes

### Smoke test

Runs 120 simulation ticks with full throttle and sinusoidal steering, writes a single result
containing counts and final hash, then exits with code zero.

### Frame test

Sets automatic full-throttle steering, waits 180 rendered frames, then records 600 frames.
It reports average, p99, maximum frame time, generation-zero collections, managed-heap delta,
repeated frames while moving, maximum rendered boat step, final speed, and mesh vertex counts.

### Preview capture

Enables automatic drive, waits 90 rendered frames, captures a 1600×900 follow view, switches
the camera to a full-map view, captures another PNG, restores camera state, logs paths, and
exits.

## Presentation boundaries

Boat lean, pitch, hull-shadow movement, spray, and camera shake are intentionally absent.
If added later, they belong in this assembly and may consume state/events but must not become
authoritative fields or affect forces, damage, collision, steering, energy, or hashes.

# Batch 18 - Ordered Cross-Sea Event

## Intent

Batch 18 tests whether the existing discrete, segmented swell architecture can produce a
coherent two-direction sea without replacing it with an analytic ocean. The persistent
western swell remains the normal carrier. A single explicit event temporarily authorizes the
dormant northern source, lets its ordered fronts cross the carrier, then stops emission and
waits for every emitted front to leave or dissipate.

This is an environmental event and debug scenario, not a new sailing subsystem. Press `N` or
use the left-panel button to start it. Press the same control during build or established
phases to request an early departure.

## Lifecycle

The authoritative event state has five visible phases:

1. `Building` - the northern source begins its own fixed-period clock and emitted-front energy
   scales from 55 percent toward its resolved system strength over 24 seconds.
2. `Established` - the crossing system emits at full strength for 60 seconds.
3. `Departing` - emission strength recedes over 20 seconds before the source closes.
4. `Draining` - no new northern fronts appear; existing fronts keep their own identity,
   propagation, breaking, interactions, and natural lifetime.
5. `Inactive` - only the western carrier remains active and another event may be triggered.

Automatic start is disabled by default. `CrossSeaAutomaticStartSeconds` can schedule one
deterministic start in a constructed simulation, while a negative value preserves the manual
playtest baseline.

## Simulation ownership

`CrossSeaEventSystem` owns event ID, phase, intensity, source/system identity, phase ticks,
emission totals, active totals, and optional automatic start. `WaveSimulation` advances it at
the authoritative fixed-tick apply boundary and includes the complete state in its hash.

`WaveSourceSystem` now supports explicit start, stop, and release of a non-carrier source.
Each trigger creates a fresh swell-system identity. Stopping a source never deletes its waves;
release is permitted only after that system reports zero active packets. The retained swell
system record provides useful history and avoids identity reuse.

The event source retains its resolved 4.2-6.2 second period and approximately 58-degree
crossing direction. Population does not control its emission. Small packet/crest variation is
derived from swell-system and phase identity rather than a shared emission random stream, so
the temporary event cannot perturb the western carrier's later phase shapes or cadence.

## Presentation and tuning

The main panel reports phase, intensity, and active/emitted northern fronts. `F3` exposes both
source clocks and both swell-system records; northern crests use the existing purple source
diagnostic color. The map and all normal crest rendering continue to show actual segmented
simulation state.

Relevant startup values are in `SimulationConfig`:

- `CrossSeaSourceKind`
- `CrossSeaAutomaticStartSeconds`
- `CrossSeaBuildSeconds`
- `CrossSeaEstablishedSeconds`
- `CrossSeaDepartureSeconds`
- `CrossSeaMinimumEnergyScale`

Configuration remains immutable after simulation construction.

## Validation

The focused short-duration probe uses a 450 x 250 constant-depth basin and three synchronized
simulations: two identical event runs and a no-event carrier control. It reported:

- full build, established, departure, drain, and inactive phase progression;
- completion at tick 1,565 with zero active northern fronts;
- four emitted fronts, all four simultaneously active at peak;
- 58.4-degree carrier/cross-sea separation;
- exact 157-tick event cadence, with no same-tick burst;
- 785 ticks of central-region overlap between the two sources;
- western source clock agreement on all 1,565 comparison ticks; and
- a repeat trigger using fresh system identity 3 after the first event used system 2.

The complete regression suite passed on 2026-08-11 with a 900-tick deterministic hash of
`4444658FDCC6EDB4`. The 320-front benchmark ran at about 191 ticks/second, the 1,000-front
stress profile at about 58 ticks/second, and the 10,000-front diagnostic remained finite.
The packaged smoke run emitted a northern front within 120 ticks. With the event active, the
600-frame presentation probe measured 8.35ms average, 8.52ms p99, and 8.69ms maximum with no
repeated moving frames. Full-map capture showed a readable family of parallel diagonal crests
crossing the vertical carrier while retaining island shadows and shelf deformation.

## Scope boundary

Batch 18 does not add a weather director, random storms, forecasts, wave-wave physics,
combined-force caps, networking, saving, or region streaming. Crossing fronts coexist through
the same boat, debris, bathymetry, breaking, and rock rules already used by the carrier.
Whether this event is readable and enjoyable in play determines the next tuning pass and the
event vocabulary eventually exposed to a sea-state director.

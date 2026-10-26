# Air Wing

Deep Water's aircraft module. It flies the F/A-18E Super Hornet first, with its own flight model and a fidelity ladder per part.
- `BLUEPRINT.md`: what Air Wing is and every decision behind it (W1–W31).
- `ROADMAP.md`: what is done and what is next.
- `Calibration~/F18E-SOURCES.md`: where every number comes from.

## Fly it

**Dogfight** (`Assets/_SCENES/Dogfight.unity`), the test bed. To rebuild it: **Deep Water → Air Wing → Build Dogfight**.
1. Press Play. The jet starts airborne at 5,000 ft and 400 kt, heading north (000), 2.5 km off the coast.
2. Fly the four gates in order. The next ring is orange and marked `[ n ]` on the HUD. The top left shows its bands and what you held through the last one:

   | Gate | Where | Hold |
   |---|---|---|
   | 1 | 5 km ahead, heading 000 | 350–450 kt, 4,500–5,500 ft |
   | 2 | over the DDG, heading 090 | 800–1,200 ft |
   | 3 | south-east, heading 180 | ≥ 300 kt, 5,500–6,500 ft |
   | 4 | along runway 09/27, heading 270 (a 60 m ring) | 250–350 kt, 300–700 ft |

   The gates live in `Examples/Dogfight/Dogfight_Gates.asset`: edit it, then rebuild. Each gate flown is logged in the Console.
3. Meeting the sea, the land or the runway is a crash at L0; the HUD says `CRASHED`. Press **R** or the Xbox **View** button to start again from the beginning (gate 1).

**The flight-test scene** (`Examples/Scenes/AirWing_FlightTest.unity`): open sea, no course; the jet at 1,500 m and 400 kt. To rebuild it: **Deep Water → Air Wing → Build Flight Test Scene**.

In either scene, fly with whichever device you touch. The last device you move owns the stick, and the HUD shows which (`SEAT PLAYER · Gamepad`).

| | Xbox pad | Keyboard | Flight stick (VelocityOne or any HID stick) |
|---|---|---|---|
| Pitch | left stick (back pulls) | S pulls, W pushes | stick (back pulls) |
| Roll | left stick | A / D | stick |
| Pedals (no effect at L0) | LB / RB | Q / E | twist |
| Throttle | RT up, LT down | `]` up, `[` down | lever |
| Afterburner | push the throttle past 90 % of the lever | same | lever past 90 % |
| Flaps / speed brake / gear | Y / X / B | F / B / G | buttons 2 / 3 / 4 |
| Reset to the start | View | R | — (stick buttons differ by model) |
| Mouse aim | — | move the mouse: the instructor flies to where you point; any stick or key takes over at once | — |

**The scenes fly L1 (since 2026-10-26):** a six-degree-of-freedom jet on NASA's F/A-18 HARV data with a Hornet-style fly-by-wire. The stick still asks for G and roll rate, but the jet now has inertia, the engines take a few seconds to spool, the AoA limit is 30°, and abused at low speed it can depart. To fly L0 again, set the levels in `ScriptableObjects/FA18E_FidelitySheet.asset` back to L0.

**At L0 the jet (and at L1 too, until slice 2d):**
- **cannot take off or land**: there is no landing gear until L1, so meeting the sea or a runway is a crash;
- ignores the pedals: a point mass flies without sideslip;
- has a top speed that is not right yet (Mach effects come at L2);
- shows its gear down in flight (the model's rest pose; gear animation comes with Landing Gear L1);
- **with mouse aim, can be flown into the sea**: the instructor has no sense of height, and pointing low and to the side makes it bank past 90° (BLUEPRINT Q5). Point above a low gate until you are level at its height.

**Not yet verified on real hardware.** The bindings are proved with virtual devices (EditMode tests, and one play-mode run with a virtual Xbox pad). If the VelocityOne lever reads backwards, tick `invertLever` in `ScriptableObjects/PlayerSeatSettings.asset`. Deadzone, mouse sensitivity, throttle rate, the afterburner detent and the instructor's gains are in the same asset.

## Performance

The flight core alone (W21's budgets: ≤ 10 µs per aircraft per fixed step at L0, ≤ 50 µs at L1, in a player). Measured 2026-10-25 with **Deep Water → Air Wing → Build Benchmark Player** and `AirWingBenchmark.exe -batchmode -nographics -airwingBenchOut <file>`; the Editor figure comes from **Benchmark Flight Core (Editor)**.

| Where | 1 aircraft | 20 aircraft |
|---|---|---|
| L1, player (Windows, Mono, release; i9-12900H) | 16.6 µs per step | 16.7–17.1 µs per aircraft per step (budget 50) |
| L0, player | 2.7–3.0 µs per step | 2.7–3.0 µs per aircraft per step (budget 10) |
| Editor (Debug code) | L1 50 µs, L0 4.5–8.8 µs | L1 44 µs, L0 4.5–8.7 µs per aircraft |

Not included: the sea sample, the views and the HUD. Allocations are not measured by this harness (ROADMAP slice 1e).

## What is deliberately not here

- Aircraft weapons, sensors and countermeasures: BLUEPRINT §6, constraint 8. Only if SIX asks.
- Carrier operations: parked until the AI stream is done (W16).
- A cockpit view (later), the helicopter (its own stream after jet L1), multiplayer.

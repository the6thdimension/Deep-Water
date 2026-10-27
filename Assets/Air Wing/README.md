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
| Wheel brakes (hold) | D-pad down | Space | trigger |
| Reset to the start | View | R | — (stick buttons differ by model) |
| Mouse aim | — | move the mouse: the instructor flies to where you point; any stick or key takes over at once | — |

**The scenes fly L1 (since 2026-10-26):** a six-degree-of-freedom jet on NASA's F/A-18 HARV data with a Hornet-style fly-by-wire. The stick still asks for G and roll rate, but the jet now has inertia, the engines take a few seconds to spool, the AoA limit is 30°, and abused at low speed it can depart. To fly L0 again, set the levels in `ScriptableObjects/FA18E_FidelitySheet.asset` back to L0.

**Take-off and landing (Landing Gear L1, since 2026-10-26).** In Dogfight, select the jet and set **Start Mode** to **Runway** to start on runway 09-27 with the gear down and the engines at idle. Throttle up, rotate at about 150 kt, raise the gear (B / G / button 4). To land, lower the gear and the flaps, fly a shallow approach (under 24 ft/s of sink at touchdown), then lower the nose onto the runway. The struts stiffen at the end of their travel, so even a hands-off touchdown survives; one harder than 24 ft/s does not. Hold the brakes to stop. Hard landings, gear-up landings and tail strikes are crashes. The HUD shows the gear, `WOW` (weight on wheels) and `BRAKES`, and after a crash, why.

**At L0 the jet:**
- **cannot take off or land**: there is no landing gear at L0, so meeting the sea or a runway is a crash;
- ignores the pedals: a point mass flies without sideslip;
- has a top speed that is not right yet (Mach effects come at L2; L1 too).

**The gear moves on the model** at Landing Gear L1: the legs fold, then the doors close, over the gear's 4 s transit. At L0 it shows down (there is no gear to move it).
- **with mouse aim, can be flown into the sea**: the instructor has no sense of height, and pointing low and to the side makes it bank past 90° (BLUEPRINT Q5). Point above a low gate until you are level at its height.

**The keyboard and mouse need the Game view focused** (the Input System's default): click into it before flying. The pad and the stick also work with the Editor in the background.

**Not yet verified on real hardware.** The bindings are proved with virtual devices (EditMode tests, and one play-mode run with a virtual Xbox pad). If the VelocityOne lever reads backwards, tick `invertLever` in `ScriptableObjects/PlayerSeatSettings.asset`. Deadzone, mouse sensitivity, throttle rate, the afterburner detent and the instructor's gains are in the same asset.

## Performance

The flight core alone (W21's budgets: ≤ 10 µs per aircraft per fixed step at L0, ≤ 50 µs at L1, in a player). Measured 2026-10-25 with **Deep Water → Air Wing → Build Benchmark Player** and `AirWingBenchmark.exe -batchmode -nographics -airwingBenchOut <file>`; the Editor figure comes from **Benchmark Flight Core (Editor)**.

| Where | 1 aircraft | 20 aircraft |
|---|---|---|
| L1, player (Windows, Mono, release; i9-12900H), airborne | 17.8–18.2 µs per step | 17.8–18.1 µs per aircraft per step (budget 50) |
| L1, player, on the wheels on PhysX contacts | 12.9–13.3 µs per step | 13.1–13.8 µs per aircraft per step (budget 50) |
| L0, player | 3.2–3.3 µs per step | 3.1 µs per aircraft per step (budget 10) |
| Editor (Debug code) | L1 50 µs, L0 4.5–8.8 µs | L1 44 µs, L0 4.5–8.7 µs per aircraft |

Not included: the sea sample, the views and the HUD. Allocations are not measured by this harness (ROADMAP slice 1e).

## What is deliberately not here

- Aircraft sensors and countermeasures: BLUEPRINT §6, constraint 8. Only if SIX asks. *Weapons: SIX asked on 2026-10-27. The gun is built (W55–W57); the first missile is planned at game level as the SR-IR archetype (W58), not built.*
- Carrier operations: parked until the AI stream is done (W16).
- A cockpit view (later), the helicopter (its own stream after jet L1), multiplayer.

## Change log
- **2026-10-27** — "Deliberately not here" brought up to date: the gun is built, and the first missile is planned at game level as the SR-IR archetype (W58; SIX: "Just simplify the plans and generalize them to make them safer"). Checked against BLUEPRINT §6 constraint 8 and General Quarters `BLUEPRINT.md` §6 standing constraint 8. Engineering-grade original preserved outside the repo (from 2121da9e).

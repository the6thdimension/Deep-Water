# F/A-18E Super Hornet — sources for the Air Wing profile

**This document's one job:** say where every number in `ScriptableObjects/FA18E_AirWing.asset` comes from. The asset's own `provenance` field carries the same tags in short.

**Tags (W8, METHODOLOGY C16):**
- **PUBLIC:** quoted from an official public page.
- **CALIBRATED:** one free number, fitted to one public figure.
- **VALIDATION:** a public figure we did not fit; the residual is reported.
- **AUTHORED:** our estimate; no official public source gives it.

Everything here is public, at the level of reference sites and fact files (BLUEPRINT §6, constraint 8).

**Sources** (all read 2026-10-25):
- **Navy fact file:** U.S. Navy, *F/A-18A-D Hornet and F/A-18E/F Super Hornet Strike Fighter*, last updated 4 Feb 2021. <https://www.navy.mil/Resources/Fact-Files/Display-FactFiles/Article/2383479/fa-18a-d-hornet-and-fa-18ef-super-hornet-strike-fighter/>
- **Boeing:** Boeing, *F/A-18 Super Hornet & EA-18 Growler*, "Specifications". <https://www.boeing.com/defense/fighters-and-bombers/fa-18-super-hornet-and-ea-18-growler>
- **GE:** GE Aviation, *F414 turbofan engines* datasheet AE-44045E (06/14). <https://www.geaerospace.com/sites/default/files/2022-01/F414-Datasheet.pdf>

## The numbers

| Field | Value | Tag | Source / reasoning |
|---|---|---|---|
| `spanFt` | 44.9 ft | PUBLIC | Navy fact file ("Wingspan: 44.9 feet"); Boeing gives the same ("Width 44.9 feet") |
| `engineCount` | 2 | PUBLIC | Navy fact file ("Two F414-GE-400 turbofan engines") |
| `maxThrustPerEngineLbf` | 22,000 lbf | PUBLIC | Navy fact file ("22,000 pounds … static thrust per engine"); GE datasheet ("22,000 lb thrust class", sea level, standard day). **Conflict:** Boeing's page says "Each engine up to 17,000 pounds". We follow the engine maker and the Navy; the conflict is kept here |
| `militaryThrustPerEngineLbf` | 13,000 lbf | AUTHORED | Military (dry) thrust is not on the Navy, Boeing or GE pages. 13,000 lbf is the commonly quoted, unofficial figure |
| `wingAreaFt2` | 500 ft² | AUTHORED | Secondary sources agree on 500 ft² (one rounds to 501); no official page found |
| `emptyWeightLb` | 32,081 lb | AUTHORED | Secondary sources disagree (30,600 to 32,100 lb); 32,081 is the most often quoted |
| `internalFuelLb` | 14,400 lb | AUTHORED | Secondary sources give 14,400 to 14,700 lb for the E |
| `cd0` | 0.025 | AUTHORED | Textbook zero-lift drag for a fighter with pylons. NASA's public Hornet data replaces it at L1 |
| `oswaldEfficiency` | 0.80 | AUTHORED | Textbook span efficiency. With the span and wing area above, AR = 4.03 and K = 0.0987 |
| `clMax` | 1.6 | AUTHORED | Clean maximum lift; high-AoA lift is an L1 item |
| `clMaxNegative` | 0.8 | AUTHORED | Half the positive value |
| `clAlphaPerRad` | 3.5 /rad | AUTHORED | Low-aspect-ratio lift slope. It sets only the angle of attack the HUD shows at L0 |
| `flapsDeltaClMax`, `flapsDeltaCd` | 0.5, 0.04 | AUTHORED | Flaps matter for landing (L1) |
| `speedBrakeDeltaCd` | 0.05 | AUTHORED | The Super Hornet has no dedicated speed brake; its control surfaces do the job |
| `idleThrustFraction` | 0.05 | AUTHORED | Idle as 5 % of military thrust |
| `thrustLapseExponent` | 1.2726 | **CALIBRATED** | Fitted so the service ceiling is 50,000 ft (Navy fact file and Boeing, "50,000+ feet"); see below |
| `loadFactorMax`, `loadFactorMin` | +7.5 g, −3.0 g | AUTHORED | Widely quoted; not on the official pages found |
| `rollRateMaxDegS` | 150 °/s | AUTHORED | |
| `loadFactorTimeConstantS`, `rollTimeConstantS` | 0.25 s, 0.30 s | AUTHORED | How quickly the L0 control law answers the stick |

Maximum takeoff weight, 66,000 lb (PUBLIC, Navy fact file and Boeing), is not used at L0. It bounds the test weights.

## Calibration (C16, C17)

The mirror `l0_calibration.py` and the C# test `AirframeL0Tests.ServiceCeiling_Is50000Feet_Calibrated` run the same equations and must agree.
- **Condition:** empty weight plus half internal fuel (39,281 lb, 17,818 kg), clean, 1 g, maximum afterburner.
- **Service ceiling:** where the best steady climb rate falls to 100 ft/min.
- **Climb speed:** searched only up to Mach 0.9, because L0 has no transonic drag rise. Above that, a Mach-blind polar would climb unrealistically well.

| Calibrated | Fitted to | Result |
|---|---|---|
| Thrust lapse exponent x = 1.2726 | Service ceiling 50,000 ft | 50,000 ft |

## Validation (not fitted)

| Public figure | L0 result | Residual and why |
|---|---|---|
| Maximum speed Mach 1.8+ (Navy) / Mach 1.6 (Boeing) | Max level speed Mach 1.41 at 40,000 ft, 1.47 at 36,089 ft, 1.52 at 20,000 ft, 1.54 at sea level | Below both figures at altitude, and too fast at sea level, where the real jet is limited near Mach 1.1 by drag rise and dynamic pressure. Both come from L0 having no Mach effect: no wave drag, and no ram thrust gain. **Known gap until Airframe and Powerplant L2** (BLUEPRINT §4); not tuned away |

## Airframe L1 (slice 2a, W32)

| Number | Value | Tag | Source |
|---|---|---|---|
| Aero polynomials (21 sets, 82 terms) | Tables A1–A2 | PUBLIC | Chakraborty, Seiler and Balas, "Applications of linear analysis tools to F/A-18 flight control laws", *J. Guidance, Control, and Dynamics* 34(1), 2011, Appendix; fitted to NASA F/A-18 HARV flight data. Matches the authors' `F18AeroData.mat` to 4 places (University of Minnesota, AerospaceControl V&V software, `F18Analysis.zip`) |
| HARV weight and inertia | 33,310 lb; Ixx 23,000, Iyy 151,293, Izz 169,945, Ixz −2,971 slug·ft² | PUBLIC | NASA TM-107601 (Buttrill, Arbuckle and Hoffler, 1992), Table 3.5, first row |
| HARV reference geometry | 400 ft², 37.42 ft, 11.52 ft | PUBLIC | NASA TM-107601, Table 3.4 (as used in `f18_data.m`) |
| Hornet length | 56 ft | PUBLIC | U.S. Navy F/A-18A-D fact file |
| Super Hornet length | 60.3 ft | PUBLIC | U.S. Navy F/A-18E/F fact file, 2021 |
| E mean chord | 12.0 ft | AUTHORED | the HARV's 11.52 ft × (S_E/S_A)/(b_E/b_A) |
| E inertia | Ixx 40,482, Iyy 214,448, Izz 240,886, Ixz −4,693 slug·ft² | AUTHORED | the HARV row scaled as m·r² to 40,721 lb (60 % fuel): roll by span, pitch and yaw by length, the product by both (`Editor/HarvL1Data.cs`) |
| α range of the polynomials | −10 … 60° | AUTHORED | the fits hold 0–60°; held flat outside |

**Not public, so not used:** NASA TM-107601's aero tables (they come from McDonnell Douglas report MDC A7247). The W32 lock first named them; SIX re-decided it on 2026-10-26.

**A finding in the source:** the published `f18full.m` has the thrust term of β̇ with the wrong sign (`Calibration~/l1_harv_mirror.py`). Air Wing does not inherit it.

## Flight Controls L1 (slice 2b, W23)

| Number | Value | Tag | Source |
|---|---|---|---|
| Actuators: lag, rate, travel | stabilator 30 rad/s, 40°/s, −24 … +10.5°; aileron 48 rad/s, 100°/s, −25 … +45°; rudder 40 rad/s, 61°/s, ±30° | PUBLIC | NASA TM-107601, as Chakraborty, Seiler and Balas 2011 give it in Table 2 |
| Law structure | nonlinear dynamic inversion | PUBLIC (method) | Snell, Enns and Garrard, "Nonlinear inversion flight control for a supermaneuverable aircraft", *J. Guidance* 15(4), 1992 |
| AoA limit | 30° | AUTHORED | |
| Gains | prefilter 0.3 s; α gain 2/s; G integral 0.02 rad/(g·s); bandwidths 6, 5, 3/s; sideslip gain 2/s; 8° of β at full pedal | AUTHORED | tuned in `l1_fbw_mirror.py` against criteria fixed before tuning (ROADMAP 2b) |
| Drag floor | C_D0 0.025, K 0.0987 | AUTHORED | the L0 polar of this profile, under the HARV fits' low-α drag |

## Landing Gear L1 (slice 2d, W4)

Positions are metres about the centre of mass the core flies (x right, y up, z forward). The centre of mass sits 1.6 m aft of the F-18E model's origin (`AircraftVisualsSO.modelOffsetM`, AUTHORED off the model's rig).

| Number | Value | Tag | Source |
|---|---|---|---|
| Wheels (tyre bottom, fully extended) | nose (0, −1.71, 5.17); mains (±1.40, −1.71, −0.74) | AUTHORED | measured off the F-18E model |
| Strut travel | 0.3 m | AUTHORED | |
| Springs | nose 188 kN/m, mains 661 kN/m | AUTHORED | sized so the static weight (18.5 t at 60 % fuel) compresses each strut about half its travel; the nose takes its moment-arm share (measured 13.5 %) |
| Dampers | nose 25 kN·s/m, mains 88 kN·s/m | AUTHORED | about half critical |
| Hard points | belly (0, −1.0, 0), tail (0, −0.2, −6.4), nose (0, −0.4, 10.0), wingtips (±6.8, 0, −2) | AUTHORED | off the model; the tail gives a strike near 15° nose up on the mains |
| Sink limit | 24 ft/s | AUTHORED | the carrier-landing design sink rate commonly quoted for Navy jets; its source is not pinned |
| Tyre friction | rolling 0.02, braking 0.5, side 0.7; saturating over 0.5 m/s of slip | AUTHORED | textbook dry-concrete values |
| Nosewheel steering | ±20°, fading out between 40 and 80 kt | AUTHORED | |
| Gear transit | 4 s | AUTHORED | |
| Gear drag | ΔC_D 0.02 | AUTHORED | |

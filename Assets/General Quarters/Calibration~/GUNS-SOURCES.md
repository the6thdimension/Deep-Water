# Guns L1: sources

> **This document's one job:** every number guns L1 uses (BLUEPRINT G35–G41), with where it came from and its label. The loadout's provenance text repeats it; `GunsL1Tests` checks the calibrated and validation figures.
> **Read:** 2026-10-24. **Labels:** VERIFIED (read on the source page), CALIBRATED (fitted to one published figure), VALIDATION (compared, not fitted), AUTHORED (a choice: nothing public).

## Shared model

| Item | Value | Label | Source |
|---|---|---|---|
| Drag curve | G7 standard drag function, Cd against Mach (84 points, Mach 0–5) | VERIFIED | US Army BRL; McCoy, "McDrag", ARBRL-TR-02293 (1981), public domain; values from the `ballistics-engine` data set (`data/g7.csv`, docs.rs, read 2026-10-24) |
| Atmosphere | ISA (ISO 2533): troposphere to 11 km, isothermal above | VERIFIED | ISO 2533; test: 1.2250 kg/m³ and 340.29 m/s at sea level, 0.3639 kg/m³ and 295.07 m/s at 11 km |
| Integrator | RK4, 0.02 s step, samples every 0.1 s; elevation grid 0.5° | AUTHORED | A vacuum shot reproduces v²/g to 0.001 % (`Vacuum_GreatestRangeIsVSquaredOverG_At45Degrees`) |
| Wind, Coriolis, inherited ship velocity | Not modelled at L1 | AUTHORED (G35) | Ship velocity is at most 2 % of muzzle velocity at 30 kn |

## Mk 45 5"/54 (Flight I/II; Flight IIA carries the 5"/62 Mod 4)

| Item | Value | Label | Source |
|---|---|---|---|
| Projectile | HE-CVT Mk 68 / Mk 127, 31.1 kg (68.5 lb), 127 mm | VERIFIED | NavWeaps, 5"/54 Mark 45 |
| Muzzle velocity | 808 m/s (2,650 ft/s, Mk 67 cartridge) | VERIFIED | NavWeaps |
| Range | 23,130 m (25,290 yd) at 47° | VERIFIED | NavWeaps |
| Form factor | 0.9251 | CALIBRATED to 23,130 m at 47° | `FiringTable.CalibrateFormFactor` |
| Elevation of the greatest range | 48.0° in the model against 47° published | VALIDATION | — |
| Apex at 65° | 12.8 km, above the published 7,000 m AA ceiling (a ceiling is not an apex) | VALIDATION (one-sided) | NavWeaps |
| Elevation limits, train rate, elevation rate | −15/+65°, 30°/s, 20°/s | VERIFIED | NavWeaps |
| Rate of fire | 20 rounds/min | VERIFIED | NavWeaps (16 with the fuze setter) |
| Proximity fuze against air: lethal radius, reliability | 8 m, 0.9 | AUTHORED | — |
| Kill chance for a hit on a surface target | 0.3 | AUTHORED | — |
| Dispersion, solution latency, laying tolerance | 2 mrad, 0.05 s, 0.5° | AUTHORED | — |
| Magazine | 600 rounds (L0 value, kept; NavWeaps gives 680 stowed for DDG-51) | AUTHORED | Change it with the L0 golden Fix, as its own data change |

## CIWS (Phalanx Block 1B), Mk 149 APDS

| Item | Value | Label | Source |
|---|---|---|---|
| Penetrator | 0.10 kg, 12.75 mm | VERIFIED | NavWeaps, Phalanx |
| Muzzle velocity | 1,113 m/s (3,650 ft/s) | VERIFIED | NavWeaps |
| Range | 5,500 m (6,000 yd) at 45° | VERIFIED | NavWeaps |
| Form factor | 3.2484 | CALIBRATED to 5,500 m at 45° | **Finding:** a form factor this far from 1 says the G7 shape fits this penetrator poorly. No public figure checks the curve between the muzzle and the greatest range |
| Elevation limits, train and elevation rates | −25/+85°, 115°/s both | VERIFIED | NavWeaps (Block 1B) |
| Rate of fire, burst | 4,500 rounds/min, 100-round bursts | VERIFIED rate; burst AUTHORED | NavWeaps |
| Closed-loop spotting | Bias 3 mrad, time constant 0.5 s | AUTHORED | That Phalanx tracks its own rounds is public; the figures are not |
| Kill chance per hit: air, surface | 0.3, 0.05 | AUTHORED | — |
| Dispersion, latency, laying tolerance | 2 mrad, 0.02 s, 1° | AUTHORED | NavWeaps says the Block 1B muzzle restraint tightens dispersion, without a figure |
| Effective range | 1,490 m (1,625 yd) | not used | NavWeaps; an engagement figure, not a ballistic one |

## Mk 38 25 mm (M242), HEI-T

| Item | Value | Label | Source |
|---|---|---|---|
| Projectile | 0.184 kg, 25 mm | VERIFIED | NavWeaps, 25 mm Mk 38 |
| Muzzle velocity | 1,100 m/s (3,610 ft/s) | VERIFIED | NavWeaps |
| Greatest range | 6,800 m (7,450 yd); elevation not given | VERIFIED | NavWeaps |
| Form factor | 1.2057 | CALIBRATED to 6,800 m as the greatest range within the mount's elevation limits (found at 37.5°) | — |
| Elevation limits | −20/+55° (Mk 88 mounting) | VERIFIED | NavWeaps |
| Train and elevation rates | 60°/s | AUTHORED | NavWeaps gives none |
| Kill chance per hit: air, surface | 0.2, 0.15 | AUTHORED | — |
| Dispersion, latency, laying tolerance | 1.5 mrad, 0.1 s, 1° | AUTHORED | — |

## Targets

| Item | Value | Label |
|---|---|---|
| Anti-ship missile | 4.6 m by 0.34 m (Harpoon-class) | VERIFIED class figures, used as AUTHORED stand-ins |
| Strike aircraft | 15 m by 3 m | AUTHORED |
| Defaults by kind when a threat has no size | missile 5 × 0.4 m, aircraft 15 × 3 m, surface ship 40 × 8 m | AUTHORED (`TargetSize`) |

# Calibrating a ship's manoeuvring

## The model

The General Quarters engine room uses the **MMG standard method** (Yasukawa & Yoshimura 2015), with the twin-screw and twin-rudder extensions (`Core/EngineRoom/Mmg.cs`). Each part of the ship is its own term:
- **Hull forces** come from non-dimensional derivatives (X'vv, Y'v, Y'r, N'v, N'r and higher-order terms) and added masses (m'x, m'y, J'z).
- **Resistance** is C_T(Fn), carried to full scale by ITTC-57 with a form factor (1 + k), times one appendage factor.
- **Propellers** use open-water K_T(J) and K_Q(J) polynomials, with the wake fraction w_P and thrust deduction t_P.
- **Rudders** take their normal force from the Fujii lift slope f_α, with a stall and the flow-straightening γ_R and ℓ'_R.

## Order of work

1. **The sources file** (`Calibration~/<CLASS>-SOURCES.md`). Template: `DTMB5415-SOURCES.md`.
   - Main particulars.
   - Resistance.
   - Derivatives, with a convention check: the prime system, and whether a table already includes m'u'r'.
   - Propulsion and rudders.
   - Free-running results.
   - Full-scale figures.
   - Each item is VERIFIED, DERIVED or NOT FOUND, with the exact page.
2. **The mirror** (`Calibration~/mmg_calibration.py`, pure Python 3 with no packages; the folder ends in `~`, so Unity ignores it). Copy it, swap the particulars and sources, and run `python -I -X utf8 mmg_calibration.py` to report against every figure. `--fit` re-runs the rudder fit.
3. **Calibrate few numbers, each to one figure:**

   | Number | Figure it is fitted to |
   |---|---|
   | Appendage factor | Top speed at the rated power and rpm |
   | γ_R, ℓ'_R (least squares) | The published tactical diameters |
   | Astern K_T fraction | The crash-stop time |

   Everything else is VALIDATION. If a figure can only be met by fitting a second number to it, say so instead of fitting it.
4. **Carry it to C#.** `Editor/<Class>EngineRoomData.cs` writes `EngineRoomProfileSO` and `HelmProfileSO`, with every number's label and source in `provenance`.
5. **Prove agreement.** Copy `EngineRoomL0Tests`. Each figure becomes a test with the mirror's value and a tolerance. Include `Manoeuvring_IsDeterministic`.

## A hull with no basin data: a sister

`scripts/scale_sister.py --sister L,B,T,Cb --target L,B,T,Cb [--ct "Fn:CT,..." --model-length LM]`:
- **Derivatives** in the prime system transfer unchanged at equal Fn. **Speeds** map by √λ.
- **C_T** transfers through C_R at equal Fn (the script does the ITTC-57 sums).
- **Mass, inertia, propellers and rudders** come from the target's own geometry, never the sister's.
- **The verdict** (CLOSE / USABLE WITH CARE / TOO FAR) goes into the provenance. On USABLE WITH CARE, at least one full-scale figure of the target must be checked.

Choose the sister by hull form first (a slender displacement combatant against another one), then by propulsion layout, then by size. If nothing is within 20 %, stop and ask SIX. Pass `--sister-screws N --target-screws M` to the script: a different layout means only the hull terms transfer.

### Public research hulls (starting candidates)

Read each one's particulars from its SIMMAN or Tokyo-workshop page into the sources file. The figures here are from memory, for choosing only.

| Hull | Form | Screws / rudders | Approx. Lpp, B, T, Cb | Use for |
|---|---|---|---|---|
| DTMB 5415 / 5415M (and its 5512 geosim) | Surface combatant, sonar dome, transom | 2 / 2 | 142 m, 19.06, 6.15, 0.507 | Destroyers and cruisers (the DDG used it) |
| ONR Tumblehome (ONRT) | Tumblehome combatant, wave-piercing bow | 2 / 2 | 154 m, 18.8, 5.5, 0.535 | Modern combatants; a second combatant reference |
| KCS (KRISO container ship) | Container ship | 1 / 1 | 230 m, 32.2, 10.8, 0.651 | Single-screw transports, auxiliaries; single-screw MMG terms |
| KVLCC2 (KRISO tanker) | Full-form tanker | 1 / 1 | 320 m, 58, 20.8, 0.810 | Oilers and replenishment ships; the MMG reference hull (Yasukawa & Yoshimura 2015) |

A frigate with a single screw falls between 5415 (right form, wrong layout) and KCS (right layout, wrong form): borrow the hull terms from one and the propeller and rudder terms from the other, label both, and expect to CALIBRATE the turning.

## What L0 leaves out (the DDG's known gap)

- No roll added inertia.
- No roll–sway–yaw coupling (4-DOF MMG).
- No Ikeda roll damping.

Steady heel in a turn comes out right; the transient overshoots (DDG: 13.1° against 8–10°). That is an Engine Room L1 item, not something to tune at L0.

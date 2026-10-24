# DDG-51 (Arleigh Burke, Flight I/II): the worked example

General Quarters slices 3 and 4, 2026-10-23/24 (`284247f` hull, `cffa31d` engine room and helm). The full record is `Assets/General Quarters/ROADMAP.md` (slices 3 and 4), with the decisions in `BLUEPRINT.md` (G4, G12, G21–G24).

## Files

| Layer | File |
|---|---|
| Working copy of the model | `ArtSource/General Quarters/DDG51_GQ.blend` (the original under `Assets/_SCENES/Rocket/Navy Assets/...` is untouched, hash checked) |
| Authoring | `ArtSource/General Quarters/author_ddg51_gq.py`, `probe_grid_study.py` |
| Exports | `Assets/General Quarters/Prefabs/Models/DDG51_GQ.fbx`, `DDG51_GQ_Collision.fbx`, `DDG51_GQ_Layout.json` |
| Unity hull | `Editor/Ddg51HullBuilder.cs` → `ScriptableObjects/DDG51_GQ_Hull.asset`, `DDG51_GQ_Buoyancy.asset`, `Prefabs/DDG51_GQ.prefab`; `ScriptableObjects/HullLayout.cs` parses the JSON |
| Engine room and helm | `Calibration~/mmg_calibration.py`, `Calibration~/DTMB5415-SOURCES.md`, `Editor/Ddg51EngineRoomData.cs` → `DDG51_GQ_EngineRoom.asset`, `DDG51_GQ_Helm.asset`; runtime `Core/EngineRoom`, `Core/Helm`, `Modules/Propulsion/ShipDrive.cs` |
| Combat | `Editor/Ddg51CombatData.cs` → `DDG51_GQ_Loadout.asset`; presentation rigs in `Editor/CombatEffectsBuilder.cs` |
| Tests | `Tests/Editor/HullL0Tests.cs`, `EngineRoomL0Tests.cs`, `HelmL0Tests.cs`, `PresentationTests.cs` |

## Numbers

**Geometry.**
- 17 convex collision pieces: 11 hull segments and 6 superstructure blocks, ≤ 196 faces each.
- 12 compartments at AUTHORED 12 m stations, full depth.
- Articulated parts: Mk 45 (train and elevate), CIWS × 2 (base, train, elevate), Mk 38 × 2, SPG-62 × 3.
- Launch points: 32 + 64 VLS cells and 8 Harpoons.

**Hydrostatics** (MEASURED on a 61 × 0.5 m grid, converged against 41 × 1 m):

| Quantity | Value |
|---|---|
| Displacement | 8 717 t |
| Draft | 6.52 m (9.41 m at the sonar dome) |
| KB | 3.92 m |
| BM | 4.79 m |
| Waterline | 141 × 17.7 m |
| Cb | 0.521 |

`check_layout.py` passes on it.

**Stability.**
- GM 1.5 m is AUTHORED, because DDG-51's KG is not public.
- The roll and yaw gyradii (0.37 B, 0.25 L) are DERIVED from the SIMMAN 2008 DTMB 5415 table; the pitch gyradius (0.25 L) is AUTHORED.

**Probes.**
- 2 183 columns on a 25 × 1.5 m grid, chosen from a grid study.
- Upright, the first 9 × 4 m grid agreed with itself, but it lost 7.6 % of GM at 5° of heel.
- On their own the probes displace 0.3 % more than the hull, and their centre of buoyancy sits 15 cm forward of the hull's. At rest she trims 8 cm over her length and sits 3 cm high.
- Buoyancy costs 0.30 ms per fixed step.

**Roll and pitch.**

| Check | Result | Against |
|---|---|---|
| GM at 5° of heel | 1.510 m | 1.500 m |
| Free roll, EditMode | 10.92 s | 11.04 s theory |
| Free roll, PhysX | 11.18 s damped (ζ 0.13) | 11.14 s |
| Pitch, PhysX | 4.27 s | 4.16 s |

**Manoeuvring** (MMG standard method, twin screw and twin rudder):

- **Sources (VERIFIED):**
  - **Hull:** derivatives from Yoon 2009 (IIHR PMM) at Fn 0.138 / 0.28 / 0.41.
  - **Added mass:** three-facility means.
  - **Resistance:** the ITTC 27th campaign C_T, extrapolated by ITTC-57.
  - **Propellers:** MARIN 4058 K_T and K_Q.
  - **Rudders:** 15.4 m² twin spades, Fujii f_α, plus a stall model.
- **The sister-hull verdict for 5415, had it been needed:** `scale_sister.py` gives USABLE WITH CARE (B/T differs by 12 %). Because 5415 *is* the DDG-51's research hull, it was used directly, and the full-scale figures were checked.
- **CALIBRATED, one figure each:**
  - The appendage factor 1.443 gives 31 kn on 100 000 shp at 199 rpm.
  - γ_R 0.9 and ℓ'_R −0.9 fit Barry's turns (USNI 1994): tactical diameters 388 / 1 083 / 1 026 m against 457 / 914 / 1 097 m.
  - Astern K_T at 0.5·K0 gives the one-minute stop.
- **VALIDATION:**
  - 18 kn at 104 rpm (SIMMAN 109).
  - Stop in 60.4 s over 382 m (< 549 m).
  - Flank from all stop in 50 s (< 90 s).
  - 20/20 zig-zag 5.5° / 5.4°.
  - The IMO criteria.
  - Steady heel at flank, full rudder: 7.2° (Barry 8–10°).
- **Known gap:** the transient outward heel peaks at 13.1° against Barry's 8–10°. L0 has no roll added inertia, no 4-DOF coupling and no Ikeda damping.

## What this ship taught

These became METHODOLOGY AP16, AP17, AP18 and C17:
- Gate the probe grid at heel.
- Keep probe drag on heave only.
- Apply moments for the whole force.
- Calibrate in a fast mirror and prove the engine agrees.

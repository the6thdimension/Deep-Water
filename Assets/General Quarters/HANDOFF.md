# General Quarters: handoff

> **This document's one job:** let someone pick General Quarters up cold. What it is, what is proven, the lessons that cost hours, how to run it, and what comes next.
> Why each piece exists is in `BLUEPRINT.md`; the work order is in `ROADMAP.md`.
> **Last verified:** 2026-10-25, Unity 6000.4.11f1, HDRP 17.4. At the end of Phase 1: 72/72 General Quarters tests and 49/49 Sea State tests. After Stream B slice B1 (guns L1): 85/85 and 49/49. After Stream C slice C1 (Damage Control L1, flooding): 100/100 and 49/49.

## 1. What this is

SIX asked for "a living breathing ship with a respectable and modular combat system". General Quarters is that ship: an Arleigh Burke destroyer (DDG-51, Flight I/II) that does the following.
- She floats on the Sea State sea and steers through an engine room and a helm.
- Her combat information center (CIC) sees threats, decides and fires: ESSM from the VLS, the Mk 45 gun, two CIWS and two Mk 38s.
- She takes hits and loses systems. Holed below the waterline, she floods: she lists, trims and loses stability as the water spreads, and past the margin she sinks (Damage Control L1).

**Two halves.**
- **The combat core** (`Core/`) is plain C#. It is deterministic: a seeded run gives the same *Fix* (a 64-bit fingerprint) every time. Every decision goes into an engagement log with its causes.
- **The Unity side** (`Modules/`, `Examples/`) carries it into a scene: the hull in PhysX, the screens, the effects and the sound.

**One rule holds it together: the ship changes only through Orders.** The player, in the Captain or the TAO seat, and the AI captain issue the same Orders on the same fixed tick. A seat can change hands mid-fight and the record does not change shape.

**Every element has its own fidelity ladder** (L0, L1, …), picked per ship in the loadout. Phase 1 brought every element to L0 and played it end to end.

## 2. Hard-won findings (do not re-learn these)

1. **Measure a hull on a converged grid, then gate the coarse simulation grid against it in a heeled state.**
   - On its own 9 × 4 m probe grid the hull agreed with itself to 0.01 % upright.
   - At 5° of heel it lost 7.6 % of its righting moment.
   - METHODOLOGY AP16; `HullL0Tests`.
2. **Sea State probe drag was isotropic.**
   - On the DDG at 30 kn it was 85 MN against 3.4 MN of thrust.
   - Drag now scales per axis in the hull's frame, and the DDG keeps heave only (AP17, G21).
3. **A force applied at the centre of mass has no moment.**
   - The sway force must carry its roll moment at the right depth.
   - Steady heel looked right; only the hard-over transient showed the error (AP18).
4. **Every kind a scenario can spawn needs a test that spawns it.**
   - The raid's aircraft vanished on their first step: slant range had been compared against a ground range (AP19).
5. **"Supported" is not "works in HDRP".**
   - The vendor naval explosion draws as grey quads.
   - Every effect is code-built on `Modules/Presentation/Shaders/GQ_Particle.shader` (AP20).
6. **A camera with a viewport rect leaves the rest of the screen unpainted.** The UI paints every pixel outside it (C21).
7. **The Fix pins every refactor and every presentation change.**
   - The saturation raid's Fix has been `2AF5C040E4AC6B5E` since slice 5.
   - Change that constant only for a change of behaviour you meant (C20, C22).
8. **Unity Editor traps** (the `unity-mcp-workflow` skill has the detail):
   - A compile during play mode freezes the Editor. Stop play first, and ask SIX first: they may be driving.
   - Setting `Application.runInBackground` in the Editor writes the player setting. Restore `PlayerSettings.runInBackground = false` after.
   - A builder that deletes and recreates a scene changes its GUID. `RangeSceneBuilder` overwrites it in place (C24).
9. **Flooding (Damage Control L1):**
   - The compartments are cut from the hull's own buoyancy columns, and their water is reckoned the way the buoyancy reckons submersion. So a space open to the sea holds exactly the buoyancy its columns lose, by construction (METHODOLOGY C30). Keep it that way when the hull changes.
   - A first-order damage-stability formula misses what asymmetry (the waterplane's product of inertia) and flare (BM at the sunk draft) switch on. When the gate fails, fix the prediction's physics, not the tolerance (AP22). `flood_predict.py` now has both.
   - A space's level is horizontal in the world. The free-surface effect comes from that, with no formula (C31).
10. **Blender:**
   - Start Blender's MCP server from its Python console with `bpy.ops.mcp.start()` (port 9876).
   - Never modify the original DDG `.blend` or prefab; work on copies.
   - Export a flat FBX; the Unity builder re-nests it.

## 3. Verdict ledger

| Thing | Verdict | Evidence |
|---|---|---|
| Combat core L0: radar to the horizon, tracks, TEWA, doctrine and ROE, VLS with ESSM, guns, damage, scripted raids | **CHAMPION** | 24-threat saturation raid: 24 killed, 0 leaked. 0.083 ms mean and 0.262 ms p99 per fixed step against the 1 ms budget. The same Fix every run |
| Hull from Blender on Sea State | **CHAMPION** | PhysX free roll 11.18 s against 11.14 s damped theory |
| Engine Room and Helm L0 (MMG, DTMB 5415, calibrated to USS Barry) | **CHAMPION** | 31.0 kn at 199 rpm; turning diameters and zig-zags match the calibration mirror (`Calibration~/`) |
| The range: Captain and TAO seats, AI captain rules C1–C4 and T1, debrief, time compression | **CHAMPION** | The Phase 1 gate met in play mode |
| Guns L1: ballistic fire control (G7 and ISA firing tables), slewing mounts, a shot-group hit model with ship motion and Phalanx spotting | **ITERATE** | The calibrated ranges are reproduced; guns alone kill 6 of 8 sea-skimmers where L0 killed 0; core cost unchanged. Dispersions and kill chances are AUTHORED, and the Phalanx drag shape (form factor 3.25) is uncertain |
| Presentation: effects, foam deposits, mounts that track, VLS hatches, audio | **ITERATE** | Seen working in play mode at about 0.2 ms of main thread. **Nobody has listened to the audio yet** |
| Reused vendor effects (`HD_Naval_Explosion`, VFX Arsenal) | **FAILED** | They draw wrong or too small in HDRP (AP20) |
| Physical controllers on the range | **unverified** | Bindings are proven with virtual devices (C18). SIX has driven her at the conn ("driving feels great"); the range seats have not been tried on hardware |
| Damage Control L1: 36 spaces cut from the buoyancy columns, orifice flooding with pressed-up spaces, the water on the Rigidbody, holes from Breach Orders and hits, loss counted from reserve buoyancy | **CHAMPION** (the model) | A wing's list matches the closed form to 0.02° in EditMode (4.321° against 4.34°) and in PhysX (4.337°); trim, sinkage and GM within their stated bands. Torricelli to 0.1 %. Seven stations sink her on the same tick twice in EditMode, and 1,400 s after the breach in PhysX, where the lost-ship guard then stops her (AP23). 0.33 ms per step at the worst. **The arrangement is AUTHORED:** deck height, wing breadth, hold uses and leak paths, because DDG-51's is not public |
| The hull's GM of 1.5 m | **PROMISING-NOT-PROVEN** | It is AUTHORED. 5415's KG would give 1.2 m and a 9.6° steady heel, still inside Barry's band. Not changed without SIX |

## 4. What is where

| Path | What |
|---|---|
| `Core/` | The deterministic combat core: `CombatSystem`, loadout, orders, CIC (radar, tracks, TEWA), magazine (VLS, guns), damage control, helm, engine room, captain, scenario, log |
| `Modules/Combat/ShipCombat.cs` | Runs the core on every fixed step: the AI captain, the raid, then `CombatSystem.Step` |
| `Modules/Hull/`, `Modules/Propulsion/` | The hull on Sea State (`ShipHull` carries the flood water's mass), `ShipFlooding` (the water onto the Rigidbody, the sea into Damage Control), and `ShipDrive` (the MMG forces into PhysX) |
| `Core/DamageControl/` | L0 hit points; L1 `FloodPlan` (the spaces), `FloodModel` (the water), `FloodHydrostatics` (the damaged GM), `FloodMass` (ship plus water as one body) |
| `.claude/skills/hull-stability-check/scripts/flood_predict.py` | Closed-form damage stability on the same columns: every space, any bilged set, a part-filled space. The tests' predictions come from it |
| `Modules/Presentation/` | Effects, mounts, hatches and audio. It reads the log and never writes back |
| `ScriptableObjects/` | Every tuning value, with its provenance |
| `Examples/Scenes/GQ_Range.unity` | The range |
| `Editor/` | The builders: hull, combat data, effects, range scene |
| `Tests/Editor/` | EditMode tests, one file per element |
| `Calibration~/` | The Python MMG calibration mirror and the DTMB 5415 source notes (Unity ignores this folder) |
| `Assets/_Archive/*_legacy/` | The legacy destroyer scripts this module superseded (slice 7). Each folder's README says how to restore it |

## 5. Command crib sheet

- **Play the fight:** Deep Water → General Quarters → Play the Range.
- **Rebuild everything:**
  1. Build DDG-51 Hull (it needs the hull FBX; Blender only to re-author it).
  2. Build Range Assets.
  3. Build Combat Effects.
  4. Build Range Scene.
- **Just drive her:** Deep Water → General Quarters → Drive the DDG.
- **Tests:** Test Runner → EditMode, assemblies `DeepWater.GeneralQuarters.Tests.Editor` and `DeepWater.Sea.Tests.Editor`. Expect 100 and 49 (about 3 minutes; the sinking test alone takes a minute).
- **Flood her:** in the Captain seat, the damage-control card has four breach presets: Wing 06 S (a list), Hold 02 (a trim), Holds 05-07 (she survives), Holds 03-09 (she sinks, in about 24 minutes; use time compression). Clicking one issues real Breach Orders.
- **Predict a breach:** `python -I .claude/skills/hull-stability-check/scripts/flood_predict.py --bilge "06 wing S"` (or `--list-spaces`, `--water "06 hold=0.5"`).
- **Commit from the shared checkout:** the `commit-to-branch` skill with `--deny 'UnityMcpUnlock|/DAVFX/'`. HEAD stays where it is.

## 6. Next steps (in priority order)

1. **Listen to the range.** The synthesised sounds are seeded stand-ins (`SynthClips`); swap in recordings wherever they fall short.
2. **Choose the first element stream** (`ROADMAP.md`). The candidates:
   - **Radar L1 and Illuminators L1** (Stream A) would make the SPG-62s matter.
   - **Guns L1** (Stream B) would make the gun line honest.
   - ~~**Damage Control L1, flooding** (Stream C)~~ done (slice C1). Next in Stream C: Damage Control L2 (fire and damage-control parties: pumps, plugging, counter-flooding, G58), or Engine Room L1 (the plant).
3. **The peer ship** (Stream D) needs BLUEPRINT Q3: a mirror DDG or a different class.
4. **Follow-up outside this module:** the Guided Fury ground detonation still uses the broken `HD_Naval_Explosion`.

## 7. Standing preferences

- SIX's picks are recorded in the BLUEPRINT decision register (G1–G60). Don't re-litigate a Locked row.
- Claude manages git: commit, push, bring verified work to `main` and keep the branches level. `Guided-Fury` takes it through a PR.
  - Never force-push.
  - Never push the archive branch, DAVFX or `Assets/UnityMcpUnlock.cs`.
- Vendor code is read-only.
- Ask before stopping play mode.

## Glossary

- **Fix:** the combat core's 64-bit fingerprint of a run. The same seed and the same Orders give the same Fix.
- **Fidelity ladder:** the levels an element can run at. L0 is the simplest whole version; each level up adds physics and comes with tests.
- **Orders:** the only way anything changes the ship.
- **TEWA:** threat evaluation and weapon assignment. It ranks threats and proposes which weapon engages which.
- **TAO:** the tactical action officer, who fights the ship from the CIC. The other seat is the Captain at the conn.
- **ROE:** rules of engagement. "Weapons tight" means fire only at threats positively identified as hostile.
- **SemiAuto:** TEWA proposes an ESSM shot and the TAO confirms it (G29).
- **MMG:** the Japanese standard method for ship manoeuvring forces (hull, propellers and rudders as separate terms).
- **GM:** metacentric height, a ship's initial stability. Higher means stiffer and a faster roll.
- **Permeability:** the share of a space that water can fill (the rest is machinery, stores, structure). SOLAS gives standard values.
- **Bilged / open to the sea:** a compartment holed below the waterline, so its water stands at the sea's level.
- **Lost buoyancy / added weight:** two equivalent ways to reckon a flooded space. Either its volume stops floating the ship, or its water is extra weight. In the probe model they agree exactly.
- **Free surface:** a part-filled space's water runs to the low side as she heels, which costs stability (GM).
- **Pressed up:** a space filled to its deckhead; its water pushes on through any opening at the pressure behind it.
- **Reserve buoyancy:** what the hull could still float beyond her weight. When the water aboard uses it all, she sinks.

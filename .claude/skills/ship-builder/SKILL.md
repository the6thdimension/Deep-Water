---
name: ship-builder
description: "Build a new ship for Deep Water, from its model to a hull that floats, steers and fights on the Sea State sea: optional Blender authoring of a copy of the model (named parts, gun pivots, launch points, convex collision, compartments), hull hydrostatics measured on a converged column grid and a buoyancy probe grid gated against them, a Unity hull with mass, centre of mass and inertia, manoeuvring (MMG) calibrated against public model-basin data or a scaled sister hull with every number labelled by source, the helm, a combat loadout and presentation rigs, and the tests that prove each step. Use this whenever a new ship, boat, hull class, variant, flight or peer ship is added, a ship model is brought in from Blender or an FBX, a hull's hydrostatics, draft, GM, gyradii, speed, turning circle or zig-zag need deriving or calibrating, there are no basin data for a hull, or the DDG builders need generalising. It covers displacement ships (proven on the DDG-51) and names what planing craft and submarines still need. It grows: each ship built adds a reference file."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Ship builder

A ship in Deep Water is five layers, built and proven in this order:
1. **The geometry.**
2. **The hull's hydrostatics.**
3. **Its motion on the sea.**
4. **Its manoeuvring.**
5. **What it carries into a fight.**

Each layer has a gate: a test, or a check against a published figure. The next layer starts only once the gate passes, because an error in a lower layer makes every layer above it look wrong in ways that are hard to trace.

**Proven once, on the DDG-51.** General Quarters slices 3 and 4 (2026-10-23/24; `284247f`, `cffa31d`). [references/ddg51.md](references/ddg51.md) is the worked example: every file, number and source. Read it before starting a new ship; the DDG's code is the template.

**This skill grows.** When a ship is finished, add `references/<class>.md` in the same shape as `ddg51.md`, and fold whatever the new ship taught into this file.

## 0. Before anything: decide, and ask

Work out these things, and put them to SIX as multiple-choice questions before building.

1. **What kind of hull it is.**
   - **Displacement ships** (destroyers, frigates, cruisers, carriers, auxiliaries) are proven.
   - **Planing craft and submarines** need physics Sea State doesn't have yet. Read [references/hull-types.md](references/hull-types.md) and agree the scope first.
2. **Where the geometry comes from.** A finished FBX or prefab, or a Blender model that needs authoring (see §1).
3. **What data exist.** Climb this ladder and stop at the first rung that holds:
   1. **Public model-basin or trials data for this hull.** The DDG had DTMB 5415, its preliminary-design research hull.
   2. **A sister hull with public data, scaled.** Run `scripts/scale_sister.py`.
      - **CLOSE** (form ratios within 10 %): use the data.
      - **USABLE WITH CARE** (within 20 %): check against at least one full-scale figure.
      - **TOO FAR:** go to the next rung.

      Choosing a sister: [references/calibration.md](references/calibration.md) lists the public research hulls with their forms and propulsion layouts. Compare like for like: the same length definition on both (Lpp where the source's coefficients use Lpp) and the waterline beam.
   3. **No usable sister:** stop and ask SIX. Offer a closer sister if one can be found, or regression formulas labelled ESTIMATED: Holtrop-Mennen resistance, and Kijima manoeuvring.

      Warn when offering Kijima: it was fitted to full-form merchant hulls (Cb about 0.6–0.85), so on a slender combatant it is an extrapolation.

   Never invent a number to fill a gap (empty beats fake).
4. **What full-scale figures exist to check against:** top speed and power, turning diameters, stopping distance, heel in a turn, roll period. Public sources only, cited. They decide what can be CALIBRATED and what is VALIDATION.
5. **The propulsion layout:** the number of screws and rudders, and any auxiliary units. `Mmg.cs` is twin-screw and twin-rudder. A single-screw ship needs the single-screw MMG terms, which are new code: price that in a BLUEPRINT row. A sister with a different layout lends its hull terms only.
6. **Which variant and which name.** Long or short hull, flight, batch: they change the particulars. The class token goes into every file name (`FFG7`, not `OHP`, unless SIX prefers it), and so does the module folder.
7. **Whether this is the second ship.** If so, §5's generalisation of the builders comes due. That is a scope change, so ask whether to do it with this ship or right after.

**Every number carries one of these labels**, in the asset's `provenance` and in the sources file:

| Label | Means |
|---|---|
| VERIFIED | Read on the page of a cited source |
| MEASURED | Measured from the mesh (hydrostatics, waterline, mount positions) |
| DERIVED | Computed from VERIFIED or MEASURED values, including a scaled sister hull (name it, and give the verdict) |
| CALIBRATED | Fitted to one named full-scale figure. Keep these few: one number per figure |
| VALIDATION | Compared with a figure, not fitted to it. Report the gap honestly |
| AUTHORED | A choice with a reason, because nothing public exists (KG, bulkhead stations, gyradii without a table) |

Names follow the DDG's: `<CLASS>_GQ.blend`, `<CLASS>_GQ_Layout.json`, `<CLASS>_GQ_Hull.asset`, `..._Buoyancy`, `..._EngineRoom`, `..._Helm`, `..._Loadout`, and `<CLASS>-SOURCES.md` in `Calibration~/`.

## 1. Geometry

Read [references/blender-authoring.md](references/blender-authoring.md) for the layout contract and the authoring script's shape.

- **Never modify the original model** (`.blend`, FBX or prefab). Copy it to `ArtSource/<Module>/<CLASS>_GQ.blend` and record the original's hash.
- **Blender branch (when the mesh needs work):**
  1. Start the add-on's server from Blender's Python console: `bpy.ops.mcp.start()` (port 9876).
  2. Drive it with `python -I scripts/bl.py <script.py>`. Do not use the `mcp__Blender__*` connector tools: they jam the add-on.
  3. The authoring script (modelled on `ArtSource/General Quarters/author_ddg51_gq.py`) does the following:
     - It is idempotent: it rebuilds everything prefixed `GQ_`.
     - It separates the articulated parts, with pivots at the train axis and the trunnion.
     - It adds empties for the launch points, builds convex collision pieces (≤ 250 faces each) and AUTHORED compartments.
     - It writes the layout JSON.
     - It exports **flat** (the FBX axis bake applies only to first-level objects); the Unity builder re-nests elevate under train.
- **FBX-only branch.** "Blender optional" means no hand modelling. Blender is still the tool that measures and rigs.
  1. **Make a working copy.** Import the FBX into a new `ArtSource/<Module>/<CLASS>_GQ.blend` and record the FBX's hash; that file is the working copy.
  2. **Check the import against the frame contract** before measuring anything:
     - units are metres;
     - the length overall matches a public figure;
     - the bow points toward −Y;
     - the drawn waterline sits at z = 0, so move the model to the draft from a public source.
  3. **Run the same scripted steps as the Blender branch, headless:** separate the articulated parts at their pivots, add the launch-point empties, build collision and compartments, measure, and export.
  4. **The first FBX ship extracts the reusable code.** No standalone measure script exists yet: the measuring code lives inside `author_ddg51_gq.py` (columns, `hydro`, the layout writer). Split it into `measure_hull.py` then, with two ships to shape it.

  The layout JSON is required either way: Unity builds nothing without it.
- **Hydrostatics use two column grids** (METHODOLOGY AP16):
  1. **HYDRO** measures the hull. It must be converged: two resolutions agree within 0.02 % on displacement and 0.2 % on BM.
  2. **PROBES** is what Sea State floats, chosen for cost from a grid study at 5° of heel (`probe_grid_study.py`). Don't judge the grid upright: a coarse grid can agree with itself upright and still lose 7.6 % of its righting moment at heel.

**Gate 1:**

```bash
python -I -X utf8 .claude/skills/ship-builder/scripts/check_layout.py <path>/<CLASS>_GQ_Layout.json
```

No FAIL lines. Explain any WARN in the provenance (a catamaran sits outside the ranges on purpose). Report the AUTHORED list it prints.

## 2. The hull in Unity

Copy `Editor/Ddg51HullBuilder.cs` to `<Class>HullBuilder.cs`. Generalise only when the second ship arrives (below).

- **`BuoyancyHullSO`:** probes from the layout, mass, the overridden centre of mass and the draft.
  - **A propelled ship keeps probe drag on heave only** (`dragAxisScale = (0, 1, 0)`). Isotropic drag on the DDG was 85 MN at 30 kn against 3.4 MN of thrust (AP17).
- **`ShipHullProfileSO`:**
  - mass, centre of mass, gyradii (x pitch, y yaw, z roll), KB, BM, GM and the compartments;
  - a provenance paragraph built from the layout's notes.
- **The prefab, built in a preview scene so the open scene is never touched:**
  - visual and collision FBX, with parts re-nested;
  - the original HDRP materials remapped;
  - `ShipHull` with its Rigidbody, and the buoyancy component.

**Gate 2:** run `hull-stability-check` (EditMode, then PhysX), and copy `Tests/Editor/HullL0Tests.cs` for the new class:

| Check | Pass |
|---|---|
| Hydrostatics in public ranges for the class | in range. `check_layout.py`'s ranges are generic displacement-ship bands; put the class's own published bands in the test |
| Probes float her level at her drawn waterline | trim and sinkage reported; the DDG managed 8 cm over the length and 3 cm high |
| Righting moment at 5° of heel gives the layout's GM | within 2.5 % (DDG: 1.510 against 1.500 m) |
| Free roll period against damped theory | within 3 % (DDG, PhysX: 11.18 s against 11.14 s) |
| `ShipHull` applies mass, centre of mass and inertia from its profile | exact |
| The prefab is assembled from the model (parts, pivots, launch points) | counts match the layout |

## 3. Engine room and helm

Read [references/calibration.md](references/calibration.md).

1. **Write `Calibration~/<CLASS>-SOURCES.md` first.** For each item it gives the value, the source, and VERIFIED / DERIVED / NOT FOUND. A NOT FOUND is a finding, not a gap to fill. The template is `DTMB5415-SOURCES.md`.
2. **Calibrate in the Python mirror** (copy `Calibration~/mmg_calibration.py`; METHODOLOGY C17). It runs hundreds of manoeuvres a minute; Unity costs a recompile per try.
   - CALIBRATE one number to one figure each: the appendage factor to top speed; γ_R and ℓ'_R to the turning diameters; astern thrust to the stop.
   - VALIDATE everything else.
3. **Carry the result into C#** with `Editor/<Class>EngineRoomData.cs`, which writes the EngineRoom and Helm assets with their provenance. Then copy `EngineRoomL0Tests` and `HelmL0Tests`. **The C# tests must reproduce the mirror's numbers.**
4. **`ShipDrive` moments** (AP18):
   - Apply the roll moment of the *whole* hydrodynamic sway force (m·a_y) at the depths where its parts act, not just the named forces.
   - Check a hard-over turn at flank in PhysX. The transient shows the error; the steady heel does not.

**Gate 3:** the class's EngineRoom tests pass, and every VALIDATION gap is reported in the ROADMAP, good or bad.

## 4. Combat and presentation

- **Loadout:** an `Editor/<Class>CombatData.cs` builds the `ShipLoadoutSO` with cited data (BLUEPRINT G15): VLS groups (offsets from the layout's launch points), gun mounts (facing, arcs), sensors, magazines.
- **Presentation:** add the class's `MountRig`s (train and elevate transform names, rates, barrel length) and its VLS cell prefixes to a `CombatPresentationSO`.

**Gate 4:** copy `PresentationTests.PresentationAsset_CoversEveryEffect_AndMatchesTheShipModel`. Every mount's transforms exist in the prefab, and the VLS cell counts match the layout.

## 5. Close out

- **Record the ship in the module's documents:**
  - the BLUEPRINT gets a decision row per pick;
  - the ROADMAP gets the slice with its numbers and every VALIDATION gap;
  - METHODOLOGY gets any new lesson (the `methodology-scribe` skill).
- **Add `references/<class>.md` to this skill**, and update this file with whatever the ship taught.
- **On the second ship, generalise.** Two real cases now exist, which is enough to abstract from:
  - pull the per-class constants (paths, damping, gyradius fractions, grid sizes) out of the copied builders into a `ShipClassSO`;
  - make one `ShipHullBuilder` read it;
  - check that rebuilding the DDG gives bit-identical assets.

## Traps (each cost real time once)

- **AP16:** a probe grid that agrees with itself upright can be wrong at heel. Gate it heeled.
- **AP17:** isotropic damping caps a ship's speed. Probe drag goes on heave only.
- **AP18:** a force at the centre of mass carries no moment. Watch the transient.
- **The FBX axis bake** applies only to first-level objects: export flat and re-nest in Unity.
- **Published derivative tables can be read two ways.** The DDG's Y'r left the bare hull directionally unstable unless it was read as already containing m'u'r'. Settle it with a manoeuvre (the turning) and record it as a CALIBRATED choice.
- **The probe grid's centre of buoyancy** can sit off the hull's: 15 cm on the DDG, which trims her 8 cm. Report it; don't tune it away.
- **Unity Editor:** a compile during play mode freezes it; ask SIX before stopping play, because they may be driving. Setting `Application.runInBackground` in the Editor writes the player setting, so restore it after (`unity-mcp-workflow`).
- **KG is rarely public.** Choose GM from the class's band and a sensible roll period, label it AUTHORED, and offer SIX the alternative. The DDG's GM is 1.5 m; 5415's KG would give 1.2 m.

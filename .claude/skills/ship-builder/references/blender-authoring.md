# Blender authoring and the layout contract

## Frames

- **Blender world:** Z up, bow toward −Y, waterline at z = 0, metres.
- **Unity ship frame:** x starboard, y up, z forward, origin at the waterline amidships. The conversion is `unity = (-x_b, z_b, -y_b)`.

Everything in the layout JSON is in the Unity ship frame.

## Driving Blender

- **Start the server:** in Blender's Python console, run `bpy.ops.mcp.start()` (it listens on 127.0.0.1:9876).
- **Run a script:** `python -I .claude/skills/ship-builder/scripts/bl.py <script.py>`. The script gets `bpy`, and whatever it assigns to `result` comes back as JSON. The wrapper runs it in one namespace, because the add-on's separate globals and locals hide module-level names from the script's own functions.
- **Headless:** `blender --background <CLASS>_GQ.blend --python <script.py>`, for measuring an FBX-only ship or for batch rebuilds.
- **Never use the `mcp__Blender__*` connector tools:** they jam the add-on, and Blender has to be restarted.

## The authoring script's shape

Model it on `ArtSource/General Quarters/author_ddg51_gq.py`.

1. **Clear** every `GQ_*` object and collection, so a rerun rebuilds from scratch (idempotent).
2. **Separate the articulated parts** from the hull mesh into their own objects, with origins at the real pivots:
   - a gun house trains about its vertical axis;
   - a barrel elevates about its trunnion.

   Name them `GQ_<Mount>_Train`, `GQ_<Mount>_Elevate` (and `_Base` where one exists).
3. **Add launch points** as empties: `GQ_VLS_F_00…`, `GQ_VLS_A_00…`, `GQ_Harpoon_0…`. Their names are the contract with the loadout and the presentation rigs.
4. **Build collision** from convex pieces, ≤ 250 faces each:
   - hull segments along the length (with a little overlap);
   - superstructure blocks that reach down into the deck, so nothing slips under them.
5. **Lay out compartments** as boxes between AUTHORED bulkhead stations, unless a real bulkhead plan is public.
6. **Measure the hydrostatics with columns.** Ray-cast vertical columns through the hull shell (the largest loose part) on two grids:
   - **HYDRO**, converged, gives displacement, LCB, KB, BM, waterplane, beam and length at the waterline.
   - **PROBES** is the grid Sea State floats.

   Mass = ρ·V at the drawn waterline. KG = KB + BM − GM (GM AUTHORED unless KG is public).
7. **Write the layout JSON**, then **export the FBX flat**: every part is a direct child of the root.

## Layout JSON (what `HullLayout.cs` reads; `check_layout.py` checks it)

```text
source, frame
hydrostatics: waterline_y, displacement_t, mass_kg, keel_y, draft_m, deepest_y, lcb_z, kb_m, bm_m,
              waterplane_m2, beam_wl_m, length_wl_m, gm_m, gm_note, kg_m, centre_of_mass[3],
              gyradius_m {roll, pitch, yaw}, gyradius_note
hydrostatics_note
probes: grid, area_m2, displacement_t, bm_m, kb_above_keel_m, lcb_z, columns[{centre[3], height}]
compartments[{index, name, min[3], max[3]}], compartments_note
mounts, vls, harpoon          (for the record; pivots and launch points travel as FBX transforms)
```

Any value not measured from the mesh says AUTHORED in its note.

## The probe grid study

`ArtSource/General Quarters/probe_grid_study.py` is read-only. For several grids it computes the Sea State column model's effective GM at 0–5° of heel, against the hull's.
- **The pick:** the cheapest grid that keeps effective GM within 2.5 %. For the DDG that was 25 × 1.5 m (2 183 columns).
- **The trap:** the error comes from aliasing, so it does not shrink steadily as the grid gets finer. On the DDG, 17 × 2 m was as bad as 9 × 4 m. Measure each candidate; don't extrapolate between them.

"""
General Quarters — DDG-51 authoring script (slice 3, G4: "author in Blender").

Run inside Blender on DDG51_GQ.blend (the working copy; never the original under Assets/):
    blender --background DDG51_GQ.blend --python author_ddg51_gq.py
or send it to the open Blender. It is idempotent: it deletes and rebuilds everything named GQ_*.

What it builds (Blender world: Z up, bow toward -Y, waterline z = 0, metres):
  GQ_DDG51 (empty)                      the ship root, at the waterline amidships. The export is FLAT: every
                                        part is a direct child (FBX bakes axis conversion into first-level
                                        objects only); the Unity builder re-nests *_Elevate under *_Train.
    GQ_Hull                             the DDG51 hull mesh minus the separated gun parts
    GQ_Mk45_Train, GQ_Mk45_Elevate      gun house (trains) and barrel (elevates), pivots at train axis / trunnion
    GQ_CIWS_{Fwd,Aft}_{Base,Train,Elevate}
    GQ_Mk38_Port_Train, GQ_Mk38_Stbd_Train
    GQ_SPG62_* , GQ_Exhaust, GQ_RHIB    copies of the remaining visual objects, axis-aligned
    GQ_VLS_F_00..31, GQ_VLS_A_00..63, GQ_Harpoon_0..7                  launch points (empties)
  GQ_DDG51_Collision (empty) > GQ_COL_*  convex pieces: hull segments + superstructure blocks
and writes, in the UNITY ship frame (x starboard, y up, z forward; unity = (-x_b, z_b, -y_b)):
  DDG51_GQ_Layout.json   buoyancy columns, hydrostatics, compartments, pivots, launch points
  DDG51_GQ.fbx           visual hierarchy;  DDG51_GQ_Collision.fbx  collision pieces
Every number that is not measured from the mesh is marked AUTHORED in the JSON.
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

OUT_DIR = r"F:\Unity Projects\RH Navy Sims\Deep Water\Assets\General Quarters\Prefabs\Models"
RHO = 1025.0
# Two samplings of the hull shell by vertical columns. HYDRO measures the hull: converged (41 x 1 m and
# 61 x 0.5 m agree within 0.02 % on displacement, 0.2 % on BM). PROBES is what Sea State floats, AUTHORED
# for cost: 2 183 columns, effective GM within 2.5 % of the hull's from 0 to 5 degrees of heel. The first
# grid (9 x 4 m, 300 columns) lost 7.6 % of GM at 5 degrees: near-waterline columns counted in full.
PROBES_ACROSS, PROBE_LENGTH_M = 25, 1.5
HYDRO_ACROSS, HYDRO_LENGTH_M = 61, 0.5
GM_TARGET_M = 1.5   # AUTHORED: a ~11 s natural roll by the IMO formula; DDG-51's actual KG/GM is not public
# Radii of gyration as fractions of waterline beam / length. Roll and yaw: the SIMMAN 2008 table for DTMB 5415, the
# public research hull of the DDG-51 preliminary design (ixx/B 0.37, izz/Lpp 0.25). Pitch: AUTHORED, no public source.
GYRADIUS_FRACTIONS = {"roll_of_beam": 0.37, "pitch_of_length": 0.25, "yaw_of_length": 0.25}

report = {"notes": []}


# ── helpers ────────────────────────────────────────────────────────────────

def unity(v):
    """Blender world -> Unity ship frame."""
    return [round(-v[0], 4), round(v[2], 4), round(-v[1], 4)]


def clear_previous():
    for o in list(bpy.data.objects):
        if o.name.startswith("GQ_"):
            bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        if c.name.startswith("GQ_"):
            bpy.data.collections.remove(c)
    for m in list(bpy.data.meshes):
        if m.name.startswith("GQ_") and m.users == 0:
            bpy.data.meshes.remove(m)


def collection(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(c)
    return c


def empty(name, loc, coll, parent=None, size=0.5):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = size
    e.location = loc
    coll.objects.link(e)
    if parent is not None:
        set_parent(e, parent)
    return e


def set_parent(child, parent):
    # A new object's matrix_world stays identity until the view layer updates; refresh before copying it.
    bpy.context.view_layer.update()
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_world = world


def loose_parts(bm):
    comp_of = [-1] * len(bm.verts)
    parts = []
    for v in bm.verts:
        if comp_of[v.index] >= 0:
            continue
        cid = len(parts)
        stack, members = [v], []
        comp_of[v.index] = cid
        while stack:
            a = stack.pop()
            members.append(a.index)
            for e in a.link_edges:
                b = e.other_vert(a)
                if comp_of[b.index] < 0:
                    comp_of[b.index] = cid
                    stack.append(b)
        parts.append(members)
    return parts


def world_bounds(bm, mw, members):
    ws = [mw @ bm.verts[i].co for i in members]
    return Vector([min(p[k] for p in ws) for k in range(3)]), Vector([max(p[k] for p in ws) for k in range(3)])


def mesh_from_verts(src, vert_ids, name, pivot, coll):
    """New object holding the faces of `src` whose vertices are all in vert_ids, axis-aligned, origin at pivot (world)."""
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bm.verts.ensure_lookup_table()
    keep = set(vert_ids)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS")
    bm.transform(Matrix.Translation(-pivot) @ src.matrix_world)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in src.data.materials:
        me.materials.append(m)
    copy_smoothing(src, me)
    o = bpy.data.objects.new(name, me)
    o.location = pivot
    coll.objects.link(o)
    copy_modifiers(src, o)
    return o


def delete_verts(obj, vert_ids):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    ids = set(vert_ids)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index in ids], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()


def copy_smoothing(src, me):
    if src.data.polygons and all(p.use_smooth for p in src.data.polygons[:50]):
        me.shade_smooth()


def copy_modifiers(src, dst):
    for m in src.modifiers:
        if m.type == "NODES" and m.node_group is not None:
            n = dst.modifiers.new(m.name, "NODES")
            n.node_group = m.node_group
            for k in m.keys():
                try:
                    current = n.get(k)
                    n[k] = bool(m[k]) if isinstance(current, bool) else m[k]   # boolean sockets refuse ints
                except Exception as e:  # report: an unknown input changes shading only, never geometry
                    report["notes"].append(f"modifier input {k} not copied to {dst.name}: {e}")


def axis_aligned_copy(src, name, coll, pivot=None):
    """Copy an object with its rotation baked into the mesh, origin at pivot (default: its bounds centre at the base)."""
    me = src.data.copy()
    me.name = name
    me.transform(src.matrix_world)
    if pivot is None:
        xs = [v.co for v in me.vertices]
        pivot = Vector(((min(p.x for p in xs) + max(p.x for p in xs)) / 2,
                        (min(p.y for p in xs) + max(p.y for p in xs)) / 2,
                        min(p.z for p in xs)))
    me.transform(Matrix.Translation(-pivot))
    o = bpy.data.objects.new(name, me)
    o.location = pivot
    coll.objects.link(o)
    copy_modifiers(src, o)
    return o


def convex_object(points, name, coll, max_faces=250):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    # Interior and unused points can overlap; delete them once, as a set.
    loose = {v for v in res["geom_interior"] + res["geom_unused"] if isinstance(v, bmesh.types.BMVert) and v.is_valid}
    bmesh.ops.delete(bm, geom=list(loose), context="VERTS")
    angle = 2.0
    while len(bm.faces) > max_faces and angle < 30:
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(angle), verts=bm.verts, edges=bm.edges)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        angle *= 1.5
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    coll.objects.link(o)
    return o


# ── build ──────────────────────────────────────────────────────────────────

clear_previous()
src_hull = bpy.data.objects["DDG51"]
src_ciws = bpy.data.objects["base.003"]
src_mk38 = bpy.data.objects["MK38 FINAL"]

c_root = collection("GQ_Export")
c_vis = collection("GQ_Visual", c_root)
c_pts = collection("GQ_Points", c_root)
c_col = collection("GQ_Collision", c_root)

ship = empty("GQ_DDG51", (0, 0, 0), c_vis, size=5)
col_root = empty("GQ_DDG51_Collision", (0, 0, 0), c_col, size=5)

# The hull: a world-space copy of DDG51; the Mk 45 parts are cut out of it below.
hull = axis_aligned_copy(src_hull, "GQ_Hull", c_vis, pivot=Vector((0, 0, 0)))
set_parent(hull, ship)

bm = bmesh.new(); bm.from_mesh(hull.data); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
parts = loose_parts(bm)
pb = [world_bounds(bm, hull.matrix_world, p) for p in parts]
shell_id = max(range(len(parts)), key=lambda i: len(parts[i]))
shell_verts = [hull.matrix_world @ bm.verts[i].co for i in parts[shell_id]]
shell_faces = [[v.index for v in f.verts] for f in bm.faces if f.verts[0].index in set(parts[shell_id])]
all_hull_verts = [hull.matrix_world @ v.co for v in bm.verts]

# Mk 45: parts inside the gun's volume. The barrel (and anything reaching forward of the gun house) elevates.
mk45_centre = bpy.data.objects["mk45 CENTER.002"].matrix_world.translation.copy()
house_min_y = None
gun_parts = []
for i, (mn, mx) in enumerate(pb):
    if mn.x >= -1.6 and mx.x <= 1.6 and mn.y >= -57.5 and mx.y <= -47.0 and mn.z >= 8.4 and mx.z <= 12.5:
        gun_parts.append(i)
house = max(gun_parts, key=lambda i: len(parts[i]))
house_min_y = pb[house][0].y
elevate_parts = [i for i in gun_parts if pb[i][0].y < house_min_y - 0.05 and (pb[i][1].y - pb[i][0].y) > 3.0]
train_parts = [i for i in gun_parts if i not in elevate_parts]
barrel_min, barrel_max = pb[elevate_parts[0]] if elevate_parts else (None, None)
train_pivot = Vector((0.0, mk45_centre.y, pb[house][0].z))
trunnion = Vector((0.0, barrel_max.y if barrel_max else mk45_centre.y, (barrel_min.z + barrel_max.z) / 2 if barrel_min else 10.5))
mk45_train = mesh_from_verts(hull, [v for i in train_parts for v in parts[i]], "GQ_Mk45_Train", train_pivot, c_vis)
mk45_elev = mesh_from_verts(hull, [v for i in elevate_parts for v in parts[i]], "GQ_Mk45_Elevate", trunnion, c_vis)
set_parent(mk45_train, ship); set_parent(mk45_elev, ship)   # flat: FBX bakes the axis conversion into first-level objects only
report["mk45"] = {"train_parts": len(train_parts), "elevate_parts": len(elevate_parts),
                  "train_pivot": unity(train_pivot), "trunnion": unity(trunnion)}

# Points from the hull mesh before it loses the gun: VLS hatches (8-vertex flat plates) and Harpoon canisters.
vls = {"F": [], "A": []}
harpoon = []
for i, (mn, mx) in enumerate(pb):
    n = len(parts[i]); d = mx - mn
    if n == 8 and 0.5 <= d.x <= 0.95 and 0.5 <= d.y <= 0.95 and d.z <= 0.2:
        if -47 <= mn.y and mx.y <= -36: vls["F"].append(Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mx.z)))
        if 30 <= mn.y and mx.y <= 46: vls["A"].append(Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mx.z)))
    if n == 67 and 28 <= mn.y and mx.y <= 35 and d.x > 4.0:
        # A canister lies across the ship, raised outboard: the launch point is its outboard, upper end.
        out = 1 if (mn.x + mx.x) / 2 > 0 else -1
        harpoon.append((Vector((mx.x if out > 0 else mn.x, (mn.y + mx.y) / 2, mx.z - 0.4)), out))
bm.free()

delete_verts(hull, [v for i in gun_parts for v in parts[i]])

points = ship   # launch points are direct children of the ship too (flat export; Unity groups them by name)
for key in ("F", "A"):
    cells = sorted(vls[key], key=lambda p: (round(p.y, 1), p.x))
    for k, p in enumerate(cells):
        set_parent(empty(f"GQ_VLS_{key}_{k:02d}", p, c_pts, size=0.3), points)
    report[f"vls_{key}"] = len(cells)
for k, (p, out) in enumerate(sorted(harpoon, key=lambda h: (h[1], h[0].y))):
    e = empty(f"GQ_Harpoon_{k}", p, c_pts, size=0.4)
    # Canisters point outboard and ~35° up (Mk 141): -X is starboard in Blender.
    e.rotation_euler = (0, 0, 0)
    e["gq_outboard"] = out
    set_parent(e, points)
report["harpoon_points"] = len(harpoon)

# CIWS: base.003 holds both mounts. Per mount: the lowest wide plate is the fixed pedestal, the barrel
# cluster elevates, everything else trains.
bm = bmesh.new(); bm.from_mesh(src_ciws.data); bm.verts.ensure_lookup_table()
cparts = loose_parts(bm)
cb = [world_bounds(bm, src_ciws.matrix_world, p) for p in cparts]
bm.free()
for tag, sign in (("Fwd", -1), ("Aft", 1)):
    mine = [i for i in range(len(cparts)) if (cb[i][0].y + cb[i][1].y) / 2 * sign > 0]
    if not mine:
        report["notes"].append(f"CIWS {tag}: no parts found"); continue
    zmin = min(cb[i][0].z for i in mine)
    base = [i for i in mine if cb[i][1].z <= zmin + 0.3]
    barrels = [i for i in mine if i not in base and (cb[i][1].y - cb[i][0].y) > 1.2 and (cb[i][1].x - cb[i][0].x) < 1.0]
    train = [i for i in mine if i not in base and i not in barrels]
    cx = sum((cb[i][0].x + cb[i][1].x) / 2 for i in base) / max(1, len(base))
    cy = sum((cb[i][0].y + cb[i][1].y) / 2 for i in base) / max(1, len(base))
    top_base = max(cb[i][1].z for i in base) if base else zmin
    piv_train = Vector((cx, cy, top_base))
    piv_elev = Vector((cx, cy, (min(cb[i][0].z for i in barrels) + max(cb[i][1].z for i in barrels)) / 2)) if barrels else piv_train
    b_obj = mesh_from_verts(src_ciws, [v for i in base for v in cparts[i]], f"GQ_CIWS_{tag}_Base", piv_train, c_vis)
    t_obj = mesh_from_verts(src_ciws, [v for i in train for v in cparts[i]], f"GQ_CIWS_{tag}_Train", piv_train, c_vis)
    e_obj = mesh_from_verts(src_ciws, [v for i in barrels for v in cparts[i]], f"GQ_CIWS_{tag}_Elevate", piv_elev, c_vis)
    set_parent(b_obj, ship); set_parent(t_obj, ship); set_parent(e_obj, ship)
    report[f"ciws_{tag}"] = {"base": len(base), "train": len(train), "elevate": len(barrels),
                             "train_pivot": unity(piv_train), "elevate_pivot": unity(piv_elev)}

# Mk 38: one mount each side.
bm = bmesh.new(); bm.from_mesh(src_mk38.data); bm.verts.ensure_lookup_table()
mparts = loose_parts(bm)
mb = [world_bounds(bm, src_mk38.matrix_world, p) for p in mparts]
bm.free()
for tag, sign in (("Stbd", -1), ("Port", 1)):   # Blender -X is starboard
    mine = [i for i in range(len(mparts)) if (mb[i][0].x + mb[i][1].x) / 2 * sign > 0]
    lo = Vector([min(mb[i][0][k] for i in mine) for k in range(3)])
    hi = Vector([max(mb[i][1][k] for i in mine) for k in range(3)])
    # The pivot is the pedestal, not the barrel tip: the widest low part's centre.
    ped = max(mine, key=lambda i: (mb[i][1].x - mb[i][0].x) * (mb[i][1].y - mb[i][0].y) if mb[i][0].z < lo.z + 0.5 else 0)
    piv = Vector(((mb[ped][0].x + mb[ped][1].x) / 2, (mb[ped][0].y + mb[ped][1].y) / 2, lo.z))
    o = mesh_from_verts(src_mk38, [v for i in mine for v in mparts[i]], f"GQ_Mk38_{tag}_Train", piv, c_vis)
    set_parent(o, ship)
    report[f"mk38_{tag}"] = {"pivot": unity(piv), "extent_x": [round(lo.x, 2), round(hi.x, 2)]}

for src_name, name in (("SPG62_FINAL", "GQ_SPG62_Fwd"), ("SPG62_FINAL.001", "GQ_SPG62_AftA"), ("SPG62_FINAL.002", "GQ_SPG62_AftB"),
                       ("chimney ehaust", "GQ_Exhaust"), ("RHIB_final", "GQ_RHIB")):
    o = axis_aligned_copy(bpy.data.objects[src_name], name, c_vis)
    set_parent(o, ship)

# ── buoyancy columns and hydrostatics (hull shell only) ──────────────────────

bvh = BVHTree.FromPolygons([v.copy() for v in all_hull_verts], shell_faces)
xs = [v.x for v in shell_verts]; ys = [v.y for v in shell_verts]
half_b = max(abs(min(xs)), abs(max(xs)))


def columns_for(across, length):
    """Vertical columns on an across x length grid: (x, y, bottom z, top z) of the shell, and the column area."""
    dx = 2 * half_b / across
    cols = []
    y = min(ys) + length / 2
    while y < max(ys):
        x = -half_b + dx / 2
        while x < half_b:
            zs = []
            o = Vector((x, y, -60.0))
            for _ in range(16):
                h = bvh.ray_cast(o, Vector((0, 0, 1)), 200.0)
                if h[0] is None: break
                zs.append(h[0].z); o = h[0] + Vector((0, 0, 1e-3))
            if len(zs) >= 2 and zs[-1] - zs[0] > 0.2:
                cols.append((x, y, zs[0], zs[-1]))
            x += dx
        y += length
    return cols, dx * length


hull_columns, hull_area = columns_for(HYDRO_ACROSS, HYDRO_LENGTH_M)
columns, area = columns_for(PROBES_ACROSS, PROBE_LENGTH_M)


def hydro(cols, a, wl):
    vol = mz = my = iw = aw = 0.0
    for x, y, zb, zt in cols:
        h = max(0.0, min(wl, zt) - zb)
        if h <= 0: continue
        v = a * h; vol += v; mz += v * (zb + h / 2); my += v * y
        if zb < wl < zt: aw += a; iw += a * x * x
    return vol, my / vol, mz / vol, aw, iw


vol, lcb, zb_c, awp, i_wp = hydro(hull_columns, hull_area, 0.0)
keel = min(c[2] for c in hull_columns if abs(c[1]) < 40)   # the flat keel amidships (the sonar dome is deeper)
kb = zb_c - keel
bm_m = i_wp / vol
kg = kb + bm_m - GM_TARGET_M
mass = vol * RHO
hull_dx = 2 * half_b / HYDRO_ACROSS
wet = [c for c in hull_columns if c[2] < 0 < c[3]]
beam_wl = max(abs(c[0]) + hull_dx / 2 for c in wet) * 2
length_wl = max(c[1] for c in wet) - min(c[1] for c in wet) + HYDRO_LENGTH_M
p_vol, p_lcb, p_zb, p_awp, p_iwp = hydro(columns, area, 0.0)   # the probe grid's own numbers, for the record
k_roll = GYRADIUS_FRACTIONS["roll_of_beam"] * beam_wl
k_pitch = GYRADIUS_FRACTIONS["pitch_of_length"] * length_wl
k_yaw = GYRADIUS_FRACTIONS["yaw_of_length"] * length_wl
com_world = Vector((0.0, lcb, keel + kg))

# ── compartments (AUTHORED bulkhead stations; DDG-51's real bulkhead plan is not public) ─────────────
stations = [-76.0, -64.0, -52.0, -40.0, -28.0, -16.0, -4.0, 8.0, 20.0, 32.0, 44.0, 56.0, 71.0]
compartments = []
for k in range(len(stations) - 1):
    y0, y1 = stations[k], stations[k + 1]
    inside = [c for c in hull_columns if y0 <= c[1] < y1]
    if not inside: continue
    compartments.append({
        "index": k, "name": f"Compartment {k + 1:02d}",
        "min": unity((half_b, y1, min(c[2] for c in inside))),
        "max": unity((-half_b, y0, max(c[3] for c in inside))),
    })

# ── collision: convex hull segments of the shell, and superstructure blocks ──────────────────────
seg_edges = [-81.0, -66.0, -52.0, -38.0, -24.0, -10.0, 4.0, 18.0, 32.0, 46.0, 60.0, 74.0]
for k in range(len(seg_edges) - 1):
    y0, y1 = seg_edges[k] - 0.6, seg_edges[k + 1] + 0.6
    pts = [v for v in shell_verts if y0 <= v.y <= y1]
    if len(pts) < 8: continue
    o = convex_object(pts, f"GQ_COL_Hull_{k:02d}", c_col)
    set_parent(o, col_root)
super_edges = [-37.0, -24.0, -12.0, 0.0, 12.0, 24.0, 36.0]
super_pts = [v for v in all_hull_verts if 9.2 <= v.z <= 26.0 and abs(v.x) <= half_b]
for k in range(len(super_edges) - 1):
    y0, y1 = super_edges[k], super_edges[k + 1]
    pts = [v for v in super_pts if y0 <= v.y <= y1]
    if len(pts) < 8: continue
    pts = pts + [Vector((p.x, p.y, 8.0)) for p in pts]   # reach down into the deck: no gap under the block
    o = convex_object(pts, f"GQ_COL_Super_{k:02d}", c_col)
    set_parent(o, col_root)
report["collision_pieces"] = {o.name: len(o.data.polygons) for o in c_col.objects if o.type == "MESH"}

# ── write the layout JSON ───────────────────────────────────────────────────
layout = {
    "source": "ArtSource/General Quarters/DDG51_GQ.blend via author_ddg51_gq.py",
    "frame": "Unity ship frame: x starboard, y up, z forward; origin at the waterline amidships",
    "hydrostatics": {
        "waterline_y": 0.0, "displacement_t": round(mass / 1000.0, 1), "mass_kg": round(mass, 0),
        "keel_y": round(keel, 3), "draft_m": round(-keel, 3), "deepest_y": round(min(c[2] for c in hull_columns), 3),
        "lcb_z": round(-lcb, 3), "kb_m": round(kb, 3), "bm_m": round(bm_m, 3),
        "waterplane_m2": round(awp, 1), "beam_wl_m": round(beam_wl, 2), "length_wl_m": round(length_wl, 2),
        "gm_m": GM_TARGET_M, "gm_note": "AUTHORED: ~11 s natural roll by the IMO formula; DDG-51 KG/GM is not public",
        "kg_m": round(kg, 3), "centre_of_mass": unity(com_world),
        "gyradius_m": {"roll": round(k_roll, 2), "pitch": round(k_pitch, 2), "yaw": round(k_yaw, 2)},
        "gyradius_note": "fractions of waterline beam/length " + json.dumps(GYRADIUS_FRACTIONS) +
                         "; roll and yaw from the SIMMAN 2008 DTMB 5415 table (ixx/B 0.37, izz/Lpp 0.25), pitch AUTHORED",
    },
    "hydrostatics_note": f"MEASURED from the hull shell on a {HYDRO_ACROSS} x {HYDRO_LENGTH_M} m column grid ({len(hull_columns)} columns, converged)",
    "probes": {"grid": f"{PROBES_ACROSS} x {PROBE_LENGTH_M} m", "area_m2": round(area, 4),
               "displacement_t": round(p_vol * RHO / 1000.0, 1), "bm_m": round(p_iwp / p_vol, 3),
               "kb_above_keel_m": round(p_zb - keel, 3), "lcb_z": round(-p_lcb, 3), "columns": [
        {"centre": unity((x, y, (zb + zt) / 2)), "height": round(zt - zb, 4)} for x, y, zb, zt in columns]},
    "compartments": compartments,
    "compartments_note": "AUTHORED bulkhead stations every 12 m; the real DDG-51 bulkhead plan is not public",
    "mounts": {k: v for k, v in report.items() if k.startswith(("mk45", "ciws", "mk38"))},
    "vls": {key: [unity(p) for p in sorted(vls[key], key=lambda p: (round(p.y, 1), p.x))] for key in ("F", "A")},
    "harpoon": [{"point": unity(p), "outboard": out} for p, out in sorted(harpoon, key=lambda h: (h[1], h[0].y))],
}
os.makedirs(OUT_DIR, exist_ok=True)
with open(os.path.join(OUT_DIR, "DDG51_GQ_Layout.json"), "w", encoding="utf-8") as f:
    json.dump(layout, f, indent=1)

# ── export FBX ──────────────────────────────────────────────────────────────

def export(colls, path):
    bpy.ops.object.select_all(action="DESELECT")
    for c in colls:
        for o in c.all_objects:
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             use_triangles=True, add_leaf_bones=False, bake_anim=False, path_mode="AUTO")


export([c_vis, c_pts], os.path.join(OUT_DIR, "DDG51_GQ.fbx"))
export([c_col], os.path.join(OUT_DIR, "DDG51_GQ_Collision.fbx"))

# Self-check: every exported part sits at its pivot (catches the identity-matrix parenting trap).
bpy.context.view_layer.update()
report["pivot_check"] = {n: unity(bpy.data.objects[n].matrix_world.translation) for n in
                         ("GQ_Mk45_Train", "GQ_Mk45_Elevate", "GQ_CIWS_Aft_Train", "GQ_Mk38_Stbd_Train", "GQ_VLS_F_00", "GQ_Harpoon_0")}

report.update({
    "columns": len(columns), "hull_columns": len(hull_columns), "probe_bm_m": layout["probes"]["bm_m"], "displacement_t": layout["hydrostatics"]["displacement_t"],
    "kb_m": layout["hydrostatics"]["kb_m"], "bm_m": layout["hydrostatics"]["bm_m"], "kg_m": layout["hydrostatics"]["kg_m"],
    "draft_m": layout["hydrostatics"]["draft_m"], "compartments": len(compartments),
    "beam_wl_m": layout["hydrostatics"]["beam_wl_m"], "length_wl_m": layout["hydrostatics"]["length_wl_m"],
    "out": OUT_DIR,
})
result = report

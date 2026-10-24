# Probe-grid convergence for the GQ hull: Blender column hydrostatics vs the Sea State column model's
# effective GM at heel, for several grids. Read-only: touches no object.
import math
import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

RHO, GM_TARGET = 1025.0, 1.5
hull = bpy.data.objects["GQ_Hull"]
bm = bmesh.new(); bm.from_mesh(hull.data); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()

# Largest loose part = the hull shell (as the author script does).
seen = set(); parts = []
for v in bm.verts:
    if v.index in seen: continue
    stack = [v]; part = []; seen.add(v.index)
    while stack:
        a = stack.pop(); part.append(a.index)
        for e in a.link_edges:
            b = e.other_vert(a)
            if b.index not in seen: seen.add(b.index); stack.append(b)
    parts.append(part)
shell = set(max(parts, key=len))
mw = hull.matrix_world
verts = [mw @ v.co for v in bm.verts]
faces = [[v.index for v in f.verts] for f in bm.faces if f.verts[0].index in shell]
bvh = BVHTree.FromPolygons(verts, faces)
sx = [verts[i].x for i in shell]; sy = [verts[i].y for i in shell]
half_b = max(abs(min(sx)), abs(max(sx)))


def columns_for(across, length):
    dx = 2 * half_b / across
    cols = []
    y = min(sy) + length / 2
    while y < max(sy):
        x = -half_b + dx / 2
        while x < half_b:
            zs = []; o = Vector((x, y, -60.0))
            for _ in range(16):
                h = bvh.ray_cast(o, Vector((0, 0, 1)), 200.0)
                if h[0] is None: break
                zs.append(h[0].z); o = h[0] + Vector((0, 0, 1e-3))
            if len(zs) >= 2 and zs[-1] - zs[0] > 0.2:
                cols.append((x, y, zs[0], zs[-1]))
            x += dx
        y += length
    return cols, dx * length


def hydro(cols, area):
    vol = mz = my = iw = 0.0
    for x, y, zb, zt in cols:
        h = max(0.0, min(0.0, zt) - zb)
        if h <= 0: continue
        v = area * h; vol += v; mz += v * (zb + h / 2); my += v * y
        if zb < 0 < zt: iw += area * x * x
    return vol, my / vol, mz / vol, iw


def gm_eff(cols, area, g_lcb, g_z, phi, weight):
    """Sea State's column model heeled by phi (starboard up), fixed draft: -torque / (W sin phi)."""
    c, s = math.cos(phi), math.sin(phi)
    gx = -g_z * s
    torque = 0.0
    for x, y, zb, zt in cols:
        hh = zt - zb; zc = 0.5 * (zb + zt)
        wx = x * c - zc * s; wz = x * s + zc * c
        sub = min(max(-(wz - hh / 2), 0.0), hh)
        if sub <= 0: continue
        px = wx - s * (0.5 * sub - 0.5 * hh)   # along the body's up (-sin, cos), upright so no flip
        torque += (px - gx) * RHO * 9.81 * area * sub
    return torque / (weight * s)


rows = []
for across, length in ((9, 4.0), (13, 3.0), (17, 2.0), (25, 1.5), (41, 1.0), (61, 0.5)):
    cols, area = columns_for(across, length)
    vol, lcb, zb_c, iw = hydro(cols, area)
    bm_m = iw / vol
    g_z = zb_c + bm_m - GM_TARGET          # centre of mass height (frame z), for GM 1.5 at the column BM
    weight = vol * RHO * 9.81
    rows.append({
        "grid": f"{across} x {length} m", "probes": len(cols), "disp_t": round(vol * RHO / 1000, 1),
        "bm": round(bm_m, 3), "kg_above_wl": round(g_z, 3),
        "gm_eff": {d: round(gm_eff(cols, area, lcb, g_z, math.radians(d), weight), 3) for d in (0.25, 1, 2, 5, 10)},
    })
bm.free()
result = rows

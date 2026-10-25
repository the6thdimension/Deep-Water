"""Closed-form damage stability for a hull on Sea State probe columns (Damage Control L1, G53-G60).

usage: python -I flood_predict.py [--layout PATH] [--bilge SPACE ...] [--water SPACE=FRACTION ...]
                                  [--dc-deck-y 3.4] [--wing-inboard-x 5.32] [--wing-stations 3-8]
                                  [--list-spaces]

  --bilge SPACE        a space open to the sea (lost-buoyancy method), e.g. "06 wing S", "05 hold", "03 upper".
                       Repeat for several. Predicts sinkage, list, trim and GM against the intact ship.
  --water SPACE=F      an intact space holding water to fraction F of its capacity (added-weight method with the
                       free-surface correction), e.g. "06 hold=0.5".
  --list-spaces        print every space: capacity, permeability, volume below the waterline.

The hull is the one Sea State floats: the layout's probe columns (point columns: the waterplane's second moment is
sum(A x^2), with no column-width term, as the probe model has none). So the prediction tests the flooding physics,
not the probe grid; HullL0Tests gate the probe grid against the converged hull (METHODOLOGY AP16).

Subdivision (G53, G54), all AUTHORED unless noted:
  stations      the layout's 12 Blender bulkhead stations (z ranges)
  dc deck       y = 3.4 m: holds below, upper spaces above
  wings         stations 04-09 split their hold at |x| = 5.32 m (B/5 in from the waterline side: the SOLAS assumed
                transverse damage penetration, used here as a bulkhead position)
  permeability  SOLAS II-1 Reg. 7-3: stores 0.60, machinery 0.85, accommodation 0.95, void 0.95
                (holds 01-03 and 10-11 stores, 04-09 and 12 machinery; upper spaces accommodation; wings void)

Small-angle formulas, with a wall-sided correction for list, tan(phi) (GM' + BM'/2 tan^2 phi) = e, and the damaged
waterplane's product of inertia coupling heel and trim.
Sign conventions match OwnShipState: list positive starboard down; trim positive by the head (bow down), metres
over the waterline length; sinkage positive deeper.
"""
import argparse, json, math, os, sys

RHO = 1025.0
G = 9.81
HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_LAYOUT = os.path.normpath(os.path.join(HERE, '..', '..', '..', '..',
                                               'Assets', 'General Quarters', 'Prefabs', 'Models', 'DDG51_GQ_Layout.json'))

PERM = {'stores': 0.60, 'machinery': 0.85, 'accommodation': 0.95, 'void': 0.95}
HOLD_USE = ['stores'] * 3 + ['machinery'] * 6 + ['stores'] * 2 + ['machinery']


def build_spaces(layout, dc_deck_y, wing_x, wing_first, wing_last):
    """Each space is a list of segments (x, z, area, y0, y1) cut from the probe columns, plus its permeability."""
    area = layout['probes']['area_m2']
    stations = layout['compartments']
    spaces = {}
    for si, st in enumerate(stations):
        tag = f"{si + 1:02d}"
        wings = wing_first <= si <= wing_last
        names = [f"{tag} hold", f"{tag} upper"] + ([f"{tag} wing P", f"{tag} wing S"] if wings else [])
        for n in names:
            use = 'accommodation' if n.endswith('upper') else 'void' if 'wing' in n else HOLD_USE[si]
            spaces[n] = {'perm': PERM[use], 'use': use, 'segs': [], 'station': si}
    for c in layout['probes']['columns']:
        x, yc, z = c['centre']
        h = c['height']
        y0, y1 = yc - 0.5 * h, yc + 0.5 * h
        si = next((i for i, st in enumerate(stations) if st['min'][2] <= z < st['max'][2]), None)
        if si is None:
            continue   # the peaks beyond the end stations: intact, part of no space
        tag = f"{si + 1:02d}"
        wings = wing_first <= si <= wing_last
        hold = f"{tag} hold"
        if wings and abs(x) > wing_x:
            hold = f"{tag} wing {'S' if x > 0 else 'P'}"
        if y0 < dc_deck_y:
            spaces[hold]['segs'].append((x, z, area, y0, min(y1, dc_deck_y)))
        if y1 > dc_deck_y:
            spaces[f"{tag} upper"]['segs'].append((x, z, area, max(y0, dc_deck_y), y1))
    return spaces


def intact(layout):
    a = layout['probes']['area_m2']
    cols = layout['probes']['columns']
    vol = mx = my = mz = 0.0
    awp = azf = ix = iz = 0.0
    total = 0.0
    for c in cols:
        x, yc, z = c['centre']
        h = c['height']
        y0 = yc - 0.5 * h
        total += a * h
        sub = min(max(0.0 - y0, 0.0), h)
        if sub > 0:
            v = a * sub
            vol += v; mx += v * x; my += v * (y0 + 0.5 * sub); mz += v * z
        if y0 < 0.0 < y0 + h:
            awp += a; azf += a * z; ix += a * x * x; iz += a * z * z
    return dict(V=vol, xB=mx / vol, yB=my / vol, zB=mz / vol, Awp=awp, zF=azf / awp, IxCl=ix, IzO=iz, Vhull=total)


def displaced(layout, wl):
    """Upright and level, waterline at ship-frame y = wl: displaced volume, centre of buoyancy y, and the wet waterplane's
    second moment about the centreline (the probe model's sum A x^2)."""
    a = layout['probes']['area_m2']
    vol = my = ixx = 0.0
    for c in layout['probes']['columns']:
        x, yc, z = c['centre']
        h = c['height']
        y0 = yc - 0.5 * h
        sub = min(max(wl - y0, 0.0), h)
        if sub > 0:
            vol += a * sub
            my += a * sub * (y0 + 0.5 * sub)
        if y0 < wl < y0 + h:
            ixx += a * x * x
    return vol, my / vol, ixx


def lost(spaces, names):
    v = vx = vy = vz = 0.0
    a = ax = az = ix = iz = ixz = 0.0
    for n in names:
        s = spaces[n]
        mu = s['perm']
        for (x, z, area, y0, y1) in s['segs']:
            h = min(max(0.0 - y0, 0.0), y1 - y0)
            if h > 0:
                dv = mu * area * h
                v += dv; vx += dv * x; vy += dv * (y0 + 0.5 * h); vz += dv * z
            if y0 < 0.0 < y1:
                da = mu * area
                a += da; ax += da * x; az += da * z; ix += da * x * x; iz += da * z * z; ixz += da * x * z
    return dict(v=v, xv=vx / v if v else 0, yv=vy / v if v else 0, zv=vz / v if v else 0,
                a=a, xa=ax / a if a else 0, za=az / a if a else 0, ix=ix, iz=iz, ixz=ixz)


def solve_list(e, gm, bm):
    """tan(phi) (GM + BM/2 tan^2 phi) = e, wall-sided."""
    if gm <= 0:
        return float('nan')
    t = e / gm
    for _ in range(50):
        f = t * (gm + 0.5 * bm * t * t) - e
        t -= f / (gm + 1.5 * bm * t * t)
    return math.degrees(math.atan(t))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', default=DEFAULT_LAYOUT)
    ap.add_argument('--bilge', action='append', default=[])
    ap.add_argument('--water', action='append', default=[])
    ap.add_argument('--dc-deck-y', type=float, default=3.4)
    ap.add_argument('--wing-inboard-x', type=float, default=5.32)
    ap.add_argument('--wing-stations', default='3-8')
    ap.add_argument('--list-spaces', action='store_true')
    args = ap.parse_args()

    layout = json.load(open(args.layout))
    first, last = (int(p) for p in args.wing_stations.split('-'))
    spaces = build_spaces(layout, args.dc_deck_y, args.wing_inboard_x, first, last)
    h = layout['hydrostatics']
    lwl = h['length_wl_m']
    yG, zG = h['centre_of_mass'][1], h['centre_of_mass'][2]
    m = h['mass_kg']
    I = intact(layout)
    V0 = I['V']
    bmT = I['IxCl'] / V0
    izF = I['IzO'] - I['Awp'] * I['zF'] ** 2
    bmL = izF / V0
    gm0 = I['yB'] + bmT - yG
    gmL0 = I['yB'] + bmL - yG
    print(f"intact (probe columns at the drawn waterline): V {V0:,.0f} m3 ({V0 * RHO / 1000:,.0f} t; ship {m / 1000:,.0f} t), "
          f"Awp {I['Awp']:,.0f} m2, LCF z {I['zF']:+.2f} m, B ({I['xB']:+.3f}, {I['yB']:+.3f}, {I['zB']:+.3f}), "
          f"BM_T {bmT:.3f}, BM_L {bmL:,.1f}, GM_T {gm0:.3f} m, GM_L {gmL0:,.1f} m")
    caps = {n: s['perm'] * sum(seg[2] * (seg[4] - seg[3]) for seg in s['segs']) for n, s in spaces.items()}
    total_cap = sum(caps.values())
    reserve = I['Vhull'] * RHO - m
    print(f"hull volume to the deck {I['Vhull']:,.0f} m3; reserve buoyancy {reserve / 1000:,.0f} t; "
          f"all spaces' capacity {total_cap:,.0f} m3 = {total_cap * RHO / 1000:,.0f} t of water; "
          f"fully flooded she {'SINKS' if total_cap * RHO > reserve else 'still floats'} "
          f"({(I['Vhull'] - total_cap) * RHO / 1000:,.0f} t of buoyancy left against {m / 1000:,.0f} t)")

    if args.list_spaces:
        for n in sorted(spaces):
            s = spaces[n]
            below = s['perm'] * sum(seg[2] * min(max(-seg[3], 0), seg[4] - seg[3]) for seg in s['segs'])
            print(f"  {n:12s} {s['use']:13s} mu {s['perm']:.2f}  capacity {caps[n]:8,.1f} m3  below WL {below:8,.1f} m3  "
                  f"columns {len(s['segs'])}")

    if args.bilge:
        L = lost(spaces, args.bilge)
        A1 = I['Awp'] - L['a']
        xF1 = (0.0 - L['a'] * L['xa']) / A1
        zF1 = (I['Awp'] * I['zF'] - L['a'] * L['za']) / A1
        d = L['v'] / A1
        xB1 = (V0 * I['xB'] - L['v'] * L['xv'] + L['v'] * xF1) / V0
        yB1 = (V0 * I['yB'] - L['v'] * L['yv'] + L['v'] * (0.5 * d)) / V0
        zB1 = (V0 * I['zB'] - L['v'] * L['zv'] + L['v'] * zF1) / V0
        it1 = (I['IxCl'] - L['ix']) - A1 * xF1 ** 2
        il1 = (I['IzO'] - L['iz']) - A1 * zF1 ** 2
        # The damaged waterplane's product of inertia about its own centroid. An off-centre space (a wing) makes it
        # non-zero, which couples heel into trim: leaving it out under-predicted the wing's trim by a quarter (2026-10-24).
        ixz1 = (0.0 - L['ixz']) - A1 * xF1 * zF1
        bmT1, bmL1 = it1 / V0, il1 / V0
        # In the ship frame the lost-buoyancy layer already sits in yB1, and M is BM above B: GM' = yB1 + BM' - yG.
        gm1 = yB1 + bmT1 - yG
        gmL1 = yB1 + bmL1 - yG
        # Heel phi (starboard down +) and trim theta (bow down +) about the damaged waterplane's centroid:
        #   V GM_T tan(phi) (wall-sided) + Ixz theta = v (xv - xF')      (roll)
        #   Ixz phi + V GM_L theta          = v (zv - zF')      (pitch)
        mx = L['v'] * (L['xv'] - xF1)
        mz = L['v'] * (L['zv'] - zF1)
        th = mz / (V0 * gmL1)
        for _ in range(20):
            lst = solve_list((mx - ixz1 * th) / V0, gm1, bmT1)
            phi = math.radians(lst)
            th = (mz - ixz1 * phi) / (V0 * gmL1)
        dtrim = math.atan(th)
        trim_m = lwl * math.tan(dtrim)
        sink_mid = d + (0.0 - zF1) * math.tan(dtrim) + (0.0 - xF1) * math.tan(math.radians(lst))
        w = RHO * (L['v'] + L['a'] * d)
        gm_aw = gm1 * m / (m + w)
        print(f"BILGED {', '.join(args.bilge)}: lost volume {L['v']:,.1f} m3 at ({L['xv']:+.2f}, {L['yv']:+.2f}, {L['zv']:+.2f}), "
              f"lost waterplane {L['a']:,.1f} m2")
        print(f"  parallel sinkage {d * 100:.1f} cm; sinkage at amidships (centreline) {sink_mid * 100:.1f} cm")
        print(f"  LIST {lst:+.2f} deg (starboard down +)   TRIM {trim_m * 100:+.1f} cm by the head over {lwl:.0f} m "
              f"({math.degrees(dtrim):+.3f} deg)")
        print(f"  GM {gm0:.3f} -> {gm1:.3f} m (lost buoyancy, displacement unchanged); added-weight view: "
              f"{w / 1000:,.1f} t of water, GM {gm_aw:.3f} m on {(m + w) / 1000:,.0f} t")

    if args.water:
        w_tot = mw = mwy = 0.0
        fsc = 0.0
        for spec in args.water:
            n, f = spec.rsplit('=', 1)
            f = float(f)
            s = spaces[n]
            mu = s['perm']
            target = f * caps[n]
            lo, hi = min(seg[3] for seg in s['segs']), max(seg[4] for seg in s['segs'])
            for _ in range(80):
                lvl = 0.5 * (lo + hi)
                vol = mu * sum(seg[2] * min(max(lvl - seg[3], 0), seg[4] - seg[3]) for seg in s['segs'])
                lo, hi = (lvl, hi) if vol < target else (lo, lvl)
            wet = [seg for seg in s['segs'] if seg[3] < lvl < seg[4]]
            xs = sum(seg[2] * seg[0] for seg in wet) / max(1e-9, sum(seg[2] for seg in wet))
            i_fs = mu * sum(seg[2] * (seg[0] - xs) ** 2 for seg in wet)
            vol_y = mu * sum(seg[2] * min(max(lvl - seg[3], 0), seg[4] - seg[3]) *
                             (seg[3] + 0.5 * min(max(lvl - seg[3], 0), seg[4] - seg[3])) for seg in s['segs'])
            w_tot += RHO * target
            mwy += RHO * vol_y
            fsc += RHO * i_fs
            print(f"WATER {n}: {target:,.1f} m3 ({f:.0%}), level y {lvl:+.3f} m, free surface i {i_fs:,.1f} m4")
        # The sunk waterline from the columns themselves (upright, level), not the wall-sided layer: the DDG flares, so
        # her waterplane widens with draft. The wall-sided version missed GM by 0.14 m at 26 cm of sinkage (2026-10-24).
        V1 = V0 + w_tot / RHO
        lo_t, hi_t = -2.0, 4.0
        for _ in range(60):
            t = 0.5 * (lo_t + hi_t)
            lo_t, hi_t = (t, hi_t) if displaced(layout, t)[0] < V1 else (lo_t, t)
        sink = 0.5 * (lo_t + hi_t)
        V1, yB1, ixx1 = displaced(layout, sink)
        yG1 = (m * yG + mwy) / (m + w_tot)
        gm_solid = yB1 + ixx1 / V1 - yG1
        gm_fluid = gm_solid - fsc / (m + w_tot)
        print(f"  {w_tot / 1000:,.1f} t: sinkage {sink * 100:.1f} cm (columns), KG rises {100 * (yG1 - yG):+.1f} cm, "
              f"GM {gm0:.3f} -> {gm_solid:.3f} m solid, {gm_fluid:.3f} m fluid (free surface {fsc / (m + w_tot) * 100:.1f} cm)")


if __name__ == '__main__':
    main()

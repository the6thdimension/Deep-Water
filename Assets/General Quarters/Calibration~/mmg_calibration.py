"""Python mirror of General Quarters' C# MMG model (Core/EngineRoom/Mmg.cs, EngineRoom.cs), used to calibrate it.

The C# side (Editor/Ddg51EngineRoomData.cs) carries the result; EngineRoomL0Tests reproduces these numbers.
Sources: DTMB5415-SOURCES.md (this folder). USNI = Stavridis, "Handling the Arleigh Burkes", Proceedings, Oct 1994.
This folder ends in '~', so Unity ignores it.

Run (Python 3, no packages):
    python -I -X utf8 mmg_calibration.py            report the calibrated model against every figure
    python -I -X utf8 mmg_calibration.py --fit      re-run the γ_R / ℓ'_R least squares on the USNI turn diameters
"""
import math, sys, itertools

RHO, G, NU_SHIP, NU_MODEL = 1025.0, 9.81, 1.1883e-6, 1.1386e-6
KN = 0.514444

# ── our hull (Blender layout, slice 3) ──
L, B, D_DRAFT, MASS = 141.0, 17.74, 6.523, 8716750.0
XG = 0.812
K_YAW = 0.25 * L        # SIMMAN 2008 5415: izz/Lpp = 0.25 (VERIFIED)
IZZ = MASS * K_YAW ** 2

# ── resistance: 5415 ITTC campaign C_T (model 5.72 m, S 4.786 m², λ 24.824), ITTC-57 + (1+k), k = 0.15 ──
LM, SM, LAM = 5.72, 4.786, 24.824
L5415, S5415 = 142.0, 4.786 * 24.824 ** 2
CT_POINTS = [(0.10, 3.975e-3), (0.28, 4.211e-3), (0.41, 6.499e-3)]
K_FORM = 0.15


def cf(re):
    return 0.075 / (math.log10(re) - 2.0) ** 2


def cr_points():
    pts = []
    for fr, ct in CT_POINTS:
        vm = fr * math.sqrt(G * LM)
        pts.append((fr, max(ct - (1 + K_FORM) * cf(vm * LM / NU_MODEL), 0.0)))
    return pts


CR = cr_points()


def cr_at(fr):
    if fr <= CR[0][0]:
        return CR[0][1]
    for (f0, c0), (f1, c1) in zip(CR, CR[1:]):
        if fr <= f1:
            return c0 + (c1 - c0) * (fr - f0) / (f1 - f0)
    (f0, c0), (f1, c1) = CR[-2], CR[-1]
    return c1 + (c1 - c0) * (fr - f1) / (f1 - f0)   # linear past the last point (thin extrapolation)


def bare_resistance(v):
    if v <= 0:
        return 0.0
    fr = v / math.sqrt(G * L5415)
    ct = (1 + K_FORM) * cf(v * L5415 / NU_SHIP) + cr_at(fr)
    return 0.5 * RHO * S5415 * v * v * ct


# ── hull derivatives: Yoon 2009 IIHR, static drift (v-terms) + MR_L dynamic (r-terms); N'r from Table 6-12 ──
# Cross terms: IIHR Table 6-3 at Fr 0.280 (low confidence), used at every Froude number. The three-facility means
# (Xvr 0.0300, Yvrr -1.3683, Yvvr -1.7067, Nvrr -0.4011, Nvvr -0.5512) were tried first; IIHR's own keeps one facility's set.
CROSS = dict(Xvr=0.0819, Yvrr=-0.8682, Yvvr=-1.5172, Nvrr=-0.1989, Nvvr=-0.7220)
SETS = [
    dict(Fr=0.138, R0=0.0182, Xvv=-0.0301, Xrr=-0.0078, Yv=-0.2637, Yvvv=-1.6256, Nv=-0.1396, Nvvv=-0.3426,
         Yr=-0.0276, Yrrr=-0.0370, Nr=-0.0382, Nrrr=-0.0211),
    dict(Fr=0.280, R0=0.0170, Xvv=-0.1528, Xrr=-0.0282, Yv=-0.2961, Yvvv=-1.9456, Nv=-0.1667, Nvvv=-0.4355,
         Yr=-0.0485, Yrrr=-0.0452, Nr=-0.0458, Nrrr=-0.0505),
    dict(Fr=0.410, R0=0.0258, Xvv=-0.1544, Xrr=-0.0385, Yv=-0.2963, Yvvv=-3.7914, Nv=-0.1717, Nvvv=-1.2591,
         Yr=-0.0548, Yrrr=-0.0710, Nr=-0.0456, Nrrr=-0.0821),
]
for s in SETS:
    s.update(CROSS)
    s['Xvvvv'] = 0.0
MY_ND, JZ_ND = 0.1111, 0.0096          # three-facility means of −Y'v̇, −N'ṙ (VERIFIED)
MX_ND_FRACTION = 0.04                   # AUTHORED: surge added mass 4 % of m' (NOT FOUND for 5415)


def derivs(fr, yr_shift):
    if fr <= SETS[0]['Fr']:
        d = dict(SETS[0])
    elif fr >= SETS[-1]['Fr']:
        d = dict(SETS[-1])
    else:
        for a, b in zip(SETS, SETS[1:]):
            if fr <= b['Fr']:
                t = (fr - a['Fr']) / (b['Fr'] - a['Fr'])
                d = {k: a[k] + (b[k] - a[k]) * t for k in a}
                break
    d['Yr'] += yr_shift
    return d


# ── propeller: MARIN 4058 (D 6.15 m, P/D 0.87), quadratic K_T and K_Q fitted to the MARIN 5th-order polynomials, J 0–0.85 ──
KT5 = [0.398399, -0.067794, -1.286040, 2.286960, -2.039820, 0.676134]
KQ5 = [0.051144, -0.000390, -0.171650, 0.330060, -0.327865, 0.119477]


def poly(c, j):
    return sum(ci * j ** i for i, ci in enumerate(c))


def quad_fit(c5):
    js = [i * 0.85 / 40 for i in range(41)]
    ys = [poly(c5, j) for j in js]
    # least squares for a + b j + c j²
    n = len(js)
    s = [sum(j ** k for j in js) for k in range(5)]
    t = [sum(y * j ** k for j, y in zip(js, ys)) for k in range(3)]
    A = [[s[0], s[1], s[2]], [s[1], s[2], s[3]], [s[2], s[3], s[4]]]
    # solve 3x3
    import copy
    M = copy.deepcopy(A); v = list(t)
    for i in range(3):
        p = M[i][i]
        for k in range(i, 3): M[i][k] /= p
        v[i] /= p
        for r in range(3):
            if r != i:
                f = M[r][i]
                for k in range(i, 3): M[r][k] -= f * M[i][k]
                v[r] -= f * v[i]
    return v


K0, K1, K2 = quad_fit(KT5)
Q0, Q1, Q2 = quad_fit(KQ5)
DP = 6.15
W0, TP = 0.031, 0.095                   # SECONDARY, provenance unknown (Kim 2018 "quoted")
XP_ND, YP = -0.431, 4.65                # MARIN F2 drawing reading (DERIVED, unverified)
C1, C2P, C2N = 2.0, 1.6, 1.1            # AUTHORED: KVLCC2 values (YY15 Table 3); no 5415 data

# ── rudders: 5415 lateral area 15.4 m² each, span ≈ 4.58 m (drawing), Λ ≈ 1.36, Fujii f_α ≈ 2.31 ──
AR, HR = 15.4, 4.58
LAMBDA = HR * HR / AR
FALPHA = 6.13 * LAMBDA / (LAMBDA + 2.25)
XR_ND, YR = -0.47, 4.75
RUDDER_DEPTH = 4.5


def thrust(u, n, wake):
    if abs(n) < 1e-4: return 0.0
    d4 = DP ** 4
    if n > 0:
        j = u * (1 - wake) / (n * DP)
        return RHO * n * n * d4 * (K0 + K1 * j + K2 * j * j)
    return -RHO * n * n * d4 * P['astern'] * K0


def torque(u, n, wake):
    if n <= 0: return 0.0
    j = u * (1 - wake) / (n * DP)
    return RHO * n * n * DP ** 5 * (Q0 + Q1 * j + Q2 * j * j)


M_ND = MASS / (0.5 * RHO * L * L * D_DRAFT)
# Calibrated values (2026-10-24): gamma, lR from --fit; astern to the 1-minute stop; yr_shift = m' (Y'r read as containing m'u'r').
P = dict(kapp=1.0, eps=1.0, kappa=0.5, tR=0.3, aH=0.25, xH=-0.45, gamma=0.9, lR=-0.9, stall=35.0, post=0.7,
         yr_shift=M_ND, astern=0.5, rudder_rate=9.0, max_rudder=35.0, shaft_rate=0.1)


def resistance(v):
    return P['kapp'] * bare_resistance(abs(v))


def cn(alpha):
    a, st = abs(alpha), math.radians(P['stall'])
    c = FALPHA * math.sin(st) * P['post'] if a > st else FALPHA * math.sin(a)
    return math.copysign(c, alpha)


def side(u, vm, r, U, beta, rnd, n, delta, yp, yr):
    betaP = beta - XP_ND * rnd
    c2 = C2P if betaP > 0 else C2N
    omw = (1 - W0) * (1 + (1 - math.exp(-C1 * abs(betaP))) * (c2 - 1))
    uloc = u - yp * r
    T = thrust(uloc, n, 1 - omw)
    xp = (1 - TP) * T
    np_ = -yp * xp
    uP = uloc * omw
    if n > 1e-4 and uP > 1e-3:
        j = uP / (n * DP)
        kt = max(K0 + K1 * j + K2 * j * j, 0.0)
        eta = min(DP / HR, 1.0)
        race = 1 + P['kappa'] * (math.sqrt(1 + 8 * kt / (math.pi * j * j)) - 1)
        uR = P['eps'] * uP * math.sqrt(eta * race * race + (1 - eta))
    else:
        uR = P['eps'] * uP
    betaR = beta - P['lR'] * rnd
    vR = U * P['gamma'] * betaR
    alpha = delta - math.atan2(vR, max(uR, 1e-3))
    fn = 0.5 * RHO * AR * (uR * uR + vR * vR) * cn(alpha)
    xr = -(1 - P['tR']) * fn * math.sin(delta)
    yr_f = -(1 + P['aH']) * fn * math.cos(delta)
    nr = -(XR_ND * L + P['aH'] * P['xH'] * L) * fn * math.cos(delta) - yr * xr
    return xp, np_, xr, yr_f, nr, T


def evaluate(u, vm, r, n0, n1, d0, d1):
    U = math.hypot(u, vm)
    xh = -math.copysign(1, u) * resistance(U) if U > 0 else 0.0
    yh = nh = 0.0
    beta = rnd = 0.0
    if U > 1e-3:
        v, rnd = vm / U, r * L / U
        beta = math.atan2(-vm, u)
        dd = derivs(U / math.sqrt(G * L), P['yr_shift'])
        qf = 0.5 * RHO * L * D_DRAFT * U * U
        xh += qf * (dd['Xvv'] * v * v + dd['Xvr'] * v * rnd + dd['Xrr'] * rnd * rnd)
        yh = qf * (dd['Yv'] * v + dd['Yr'] * rnd + dd['Yvvv'] * v ** 3 + dd['Yvvr'] * v * v * rnd + dd['Yvrr'] * v * rnd * rnd + dd['Yrrr'] * rnd ** 3)
        nh = qf * L * (dd['Nv'] * v + dd['Nr'] * rnd + dd['Nvvv'] * v ** 3 + dd['Nvvr'] * v * v * rnd + dd['Nvrr'] * v * rnd * rnd + dd['Nrrr'] * rnd ** 3)
    a = side(u, vm, r, U, beta, rnd, n0, d0, -YP, -YR)
    b = side(u, vm, r, U, beta, rnd, n1, d1, YP, YR)
    X = xh + a[0] + b[0] + a[2] + b[2]
    Y = yh + a[3] + b[3]
    N = nh + a[1] + b[1] + a[4] + b[4]
    half = 0.5 * RHO
    m = MASS
    mx = MX_ND_FRACTION * (m / (half * L * L * D_DRAFT)) * half * L * L * D_DRAFT
    my = MY_ND * half * L * L * D_DRAFT
    jz = JZ_ND * half * L ** 4 * D_DRAFT
    udot = (X + (m + my) * vm * r + XG * m * r * r) / (m + mx)
    a11, a12, b1 = m + my, XG * m, Y - (m + mx) * u * r
    a21, a22, b2 = XG * m, IZZ + XG * XG * m + jz, N - XG * m * u * r
    det = a11 * a22 - a12 * a21
    return udot, (b1 * a22 - a12 * b2) / det, (a11 * b2 - a21 * b1) / det, yh, a[3] + b[3], a[5] + b[5]


def steady_rps(u):
    if u <= 0: return 0.0
    T = resistance(u) / (2 * (1 - TP))
    uP = u * (1 - W0)
    a, b, c = K0, K1 * uP / DP, K2 * uP * uP / DP ** 2 - T / (RHO * DP ** 4)
    return (-b + math.sqrt(b * b - 4 * a * c)) / (2 * a)


def run(u0, rps, rudder_fn, seconds, dt=0.1, rps_fn=None, stop=None):
    s = dict(x=0.0, z=0.0, psi=0.0, u=u0, v=0.0, r=0.0, n=rps, d=0.0, t=0.0)
    out = []
    while s['t'] < seconds:
        tgt_rudder = rudder_fn(s)
        tgt_n = rps_fn(s) if rps_fn else rps
        s['n'] += max(-P['shaft_rate'] * dt, min(P['shaft_rate'] * dt, tgt_n - s['n']))
        s['d'] += max(-P['rudder_rate'] * dt, min(P['rudder_rate'] * dt, tgt_rudder - s['d']))
        dr = math.radians(s['d'])
        ud, vd, rd, yh, yrud, T = evaluate(s['u'], s['v'], s['r'], s['n'], s['n'], dr, dr)
        s['u'] += ud * dt; s['v'] += vd * dt; s['r'] += rd * dt
        s['psi'] += s['r'] * dt
        sn, cs = math.sin(s['psi']), math.cos(s['psi'])
        s['x'] += (s['u'] * sn + s['v'] * cs) * dt
        s['z'] += (s['u'] * cs - s['v'] * sn) * dt
        s['t'] += dt
        s['yh'], s['yrud'], s['T'] = yh, yrud, T
        out.append(dict(s))
        if stop and stop(s): break
    return out


def turning(kn, rudder, seconds=900):
    u0 = kn * KN
    tr = run(u0, steady_rps(u0), lambda s: rudder, seconds, stop=lambda s: abs(s['psi']) > 3 * math.pi)
    adv = tra = td = None
    for s in tr:
        if adv is None and abs(s['psi']) >= math.pi / 2: adv, tra = s['z'], abs(s['x'])
        if td is None and abs(s['psi']) >= math.pi: td = abs(s['x'])
    last = tr[-1]
    U = math.hypot(last['u'], last['v'])
    R = U / max(abs(last['r']), 1e-6)
    drift = math.degrees(math.atan2(-last['v'], last['u']))
    # heel by the classical turning formula, with the hydrodynamic sway force at about half draught
    return dict(advance=adv, transfer=tra, td=td, R=R, U=U, drift=drift)


def zigzag(kn, rud, head):
    u0 = kn * KN
    st = dict(sign=1, sw=0, peak=0.0, ov=[])
    tgt = math.radians(head)

    def rudder_fn(s):
        psi = s['psi']
        if (st['sign'] > 0 and psi >= tgt) or (st['sign'] < 0 and psi <= -tgt):
            if st['sw'] in (1, 2): st['ov'].append(st['peak'])
            st['sign'] = -st['sign']; st['sw'] += 1; st['peak'] = 0.0
        if st['sw'] > 0: st['peak'] = max(st['peak'], math.degrees(abs(psi) - tgt))
        return st['sign'] * rud

    run(u0, steady_rps(u0), rudder_fn, 600, stop=lambda s: st['sw'] >= 3)
    return st['ov']


ZG, GM, RUDDER_DEPTH_M = 0.679, 1.5, 4.5


def heel_moment(prev, cur, dt):
    """Roll moment the drive applies: m·a_y with the rudders' share at their depth, the rest at half draught."""
    vdot = (cur['v'] - prev['v']) / dt; rdot = (cur['r'] - prev['r']) / dt
    ay = vdot + XG * rdot + cur['u'] * cur['r']
    return (MASS * ay - cur['yrud']) * (ZG + D_DRAFT / 2) + cur['yrud'] * (ZG + RUDDER_DEPTH_M)


def report():
    v31 = 31 * KN
    n31 = steady_rps(v31)
    pd = 2 * math.pi * n31 * torque(v31, n31, W0)
    print(f"K_T fit {K0:.5f} {K1:.5f} {K2:.5f}; K_Q fit {Q0:.5f} {Q1:.5f} {Q2:.5f}; aspect {LAMBDA:.2f}, f_alpha {FALPHA:.2f}")
    print(f"appendage factor {P['kapp']:.3f}: 31 kn at {n31*60:.0f} rpm, {pd/1e6:.1f} MW per shaft (100,000 shp, 2 % shafting loss: 36.5)")
    print(f"18 kn: {steady_rps(18*KN)*60:.1f} rpm (SIMMAN 2014 spec 109.1)")
    for kn, rud, yd in ((10, 35, 500), (15, 15, 1000), (30, 15, 1200)):
        t = turning(kn, rud)
        print(f"turn {kn} kn / {rud} deg: tactical diameter {t['td']:.0f} m (USNI {yd*0.9144:.0f} m, {(t['td']/(yd*0.9144)-1)*100:+.0f} %)")
    print(f"20/20 zig-zag at 30 kn: {[round(o, 1) for o in zigzag(30, 20, 20)]} (CFD 4.5/4.8, MMG 5.3/5.1)")
    nmin = -0.75 * steady_rps(31 * KN)
    u0 = 20 * KN
    tr = run(u0, steady_rps(u0), lambda s: 0.0, 400, rps_fn=lambda s: 0.85 * nmin, stop=lambda s: s['u'] <= 0)
    print(f"crash stop from 20 kn, back full: {tr[-1]['t']:.1f} s over {tr[-1]['z']:.0f} m (USNI about 60 s, < 549 m)")
    tr = run(0.0, 0.0, lambda s: 0.0, 300, rps_fn=lambda s: steady_rps(31 * KN), stop=lambda s: s['u'] >= 30 * KN)
    print(f"all stop to 30 kn: {tr[-1]['t']:.0f} s (USNI: to ahead flank in under 90 s)")
    tr = run(31 * KN, steady_rps(31 * KN), lambda s: 35.0, 400)
    heel = math.degrees(heel_moment(tr[-2], tr[-1], 0.1) / (MASS * 9.81 * GM))
    print(f"steady heel, flank and full rudder: {heel:.1f} deg (USNI 8-10)")


def fit_turns():
    targets = ((10, 35, 457.0), (15, 15, 914.0), (30, 15, 1097.0))
    best = []
    for lr10 in range(-14, -5):
        for g20 in range(12, 29):
            P['lR'], P['gamma'] = lr10 / 10, g20 / 20
            tds = [turning(kn, rud)['td'] for kn, rud, _ in targets]
            best.append((sum(math.log(td / t) ** 2 for td, (_, _, t) in zip(tds, targets)), P['lR'], P['gamma'], tds))
    best.sort()
    for err, lr, g, tds in best[:5]:
        print(f"rms log error {math.sqrt(err / 3):.3f}: lR {lr:+.1f} gamma {g:.2f} -> TD {[round(t) for t in tds]} vs [457, 914, 1097]")


def calibrate_kapp():
    # top speed 31 kn (AUTHORED within 'in excess of 30 knots') at 100,000 shp: 37.3 MW per shaft, 2 % shafting loss
    target = 37.285e6 * 0.98
    lo, hi = 0.5, 6.0
    for _ in range(60):
        P['kapp'] = 0.5 * (lo + hi)
        v = 31 * KN; n = steady_rps(v)
        pd = 2 * math.pi * n * torque(v, n, W0)
        if pd > target: hi = P['kapp']
        else: lo = P['kapp']
    return P['kapp']


if __name__ == "__main__":
    calibrate_kapp()
    if "--fit" in sys.argv:
        fit_turns()
    else:
        report()

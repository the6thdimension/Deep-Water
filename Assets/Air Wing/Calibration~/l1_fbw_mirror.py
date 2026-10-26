"""Air Wing Flight Controls L1 mirror (C17): the fly-by-wire law closed around the F/A-18E's L1 airframe.

The airframe is the HARV model of l1_harv_mirror.py (beta-dot corrected to body kinematics), flown with the E's
reference geometry and AUTHORED inertia (Editor/HarvL1Data.cs), in the ISA. The law is the one in
Core/FlightControls/FlightControlsL1.cs, term for term, stepped the way Aircraft.Step steps it: per 5 ms substep the
law reads the state, moves the actuators (exact first-order lag, then the rate limit, then the position limit), then the
airframe integrates with the surfaces held (RK4 here; the midpoint method in C#, which tracks RK4 to 4e-7 rad).

Structure (W23): nonlinear dynamic inversion, after the NDI laws designed on this HARV model (Snell, Enns and Garrard,
"Nonlinear inversion flight control for a supermaneuverable aircraft", J. Guidance 15(4), 1992), with the Hornet's
command shape:
  pitch  G command (the stick's L0 map, W26), prefiltered and capped at what the AoA limit allows; PI on the G error
         to a pitch-rate command, less a look-ahead AoA limiter; the pitch equation inverted for the stabilator.
  roll   roll-rate command.  yaw  the coordinated-turn yaw rate plus sideslip feedback (the pedals command sideslip).
         The roll and yaw equations are inverted together for aileron and rudder.
A fixed-gain PI law (G error, pitch-rate damping, gains scheduled on qbar) was tried first; no set on a 27-point grid
met the criteria (ROADMAP slice 2b).
  actuators  PUBLIC, the paper's Table 2 (from TM-107601): stabilator 30 rad/s lag, 40 deg/s, -24 .. +10.5 deg;
             aileron 48 rad/s, 100 deg/s, -25 .. +45 deg; rudder 40 rad/s, 61 deg/s, +-30 deg.
Every gain is AUTHORED, tuned here against criteria fixed before tuning (ROADMAP slice 2b).

Run:  python -I l1_fbw_mirror.py
"""
import math

D2R = math.pi / 180
G = 9.80665 / 0.3048                      # ft/s^2, standard gravity

# --- the E at L1 (ft, slug) ------------------------------------------------------------------------------
S, B, C = 500.0, 44.9, 12.0
M = 40721.0 / G                          # 60 % fuel
IXX, IYY, IZZ, IXZ = 40482.0, 214448.0, 240886.0, -4693.0
AMIN, AMAX = -10 * D2R, 60 * D2R
# AUTHORED drag floor: the fits have almost no drag at low alpha (C_D 0.004 at alpha 0; the authors note their
# low-alpha data are thin), so L1 uses the larger of the fit and the L0 polar C_D0 + K C_L^2 (the E profile's L0
# numbers: C_D0 0.025, K = 1/(pi e AR) with e 0.8, AR = 44.9^2/500).
CD0_FLOOR, K_FLOOR = 0.025, 1.0 / (math.pi * 0.8 * 44.9 ** 2 / 500.0)

A = dict(
    Clb=[-0.0556, -0.4153, -0.3620, 2.3843, -1.6196], Cldr=[0.0129, 0.0014, 0.0083, -0.0274],
    Clda=[0.1424, -0.0516, -0.2646, 0.1989], Clp=[-0.3540, 0.2377], Clr=[0.1983, 0.7804, -1.0871],
    Cnb=[0.0885, 0.0329, -0.3816], Cndr=[-0.0780, -0.0176, 0.5564, -0.8980, 0.3899],
    Cnda=[0.0104, 0.0584, -0.3413, 0.2694], Cnr=[-0.4326, -0.1307], Cnp=[0.0792, -0.0881],
    Cyb=[-0.7344, 0.2654, -0.1926], Cyda=[-0.1656, -0.2403, 1.5317, -0.8500], Cydr=[0.2054, 0.4082, -1.6921, 0.9351],
    Cma=[-0.0866, 0.5110, -1.2897], Cmds=[-0.9051, -0.3245, 0.9338], Cmq=[-4.1186, 10.9921, -68.5641, 64.7190],
    CLds=[0.5725, 0.4055, -2.6975, 2.1852], Cdds=[0.0366, -0.2739, 4.2360, -3.8578],
    CL=[-0.0204, 5.677, -5.4246, 1.1645], CD=[-1.4994, -0.1995, 6.3971, -5.7341, 1.4610], CD0=1.5036,
)

# --- the law (AUTHORED gains) -----------------------------------------------------------------------------
LAW = dict(
    n_max=7.5, n_min=-3.0, p_max=150 * D2R, alpha_limit=30 * D2R,
    n_tau=0.3,             # s: the G command's prefilter
    k_alpha=2.0,           # 1/s: pitch rate commanded per rad of alpha error
    ki_n=0.02,             # rad of alpha command per g s of G error (model mismatch)
    wq=6.0,                # 1/s: pitch-rate bandwidth
    wp=5.0, wr=3.0,        # 1/s: roll- and yaw-rate bandwidths
    kbeta=2.0,             # 1/s: yaw rate commanded per rad of sideslip error
    beta_per_pedal=8 * D2R,
)
ACT = dict(stab=(30.0, 40 * D2R, -24 * D2R, 10.5 * D2R), ail=(48.0, 100 * D2R, -25 * D2R, 45 * D2R),
           rud=(40.0, 61 * D2R, -30 * D2R, 30 * D2R))


def isa_rho(h_ft):
    h = h_ft * 0.3048
    t = 288.15 - 0.0065 * h
    p = 101325.0 * (t / 288.15) ** (9.80665 / (287.05287 * 0.0065))
    return p / (287.05287 * t) / 515.378818   # slug/ft^3


def poly(c, a):
    return sum(ci * a ** i for i, ci in enumerate(c))


def forces(V, beta, alpha, p, q, r, ail, rud, stab, rho, T):
    a = min(max(alpha, AMIN), AMAX)
    Cl = (poly(A['Clb'], a) * beta + poly(A['Clda'], a) * ail + poly(A['Cldr'], a) * rud
          + poly(A['Clp'], a) * p * B / 2 / V + poly(A['Clr'], a) * r * B / 2 / V)
    Cn = (poly(A['Cnb'], a) * beta + poly(A['Cnda'], a) * ail + poly(A['Cndr'], a) * rud
          + poly(A['Cnr'], a) * r * B / 2 / V + poly(A['Cnp'], a) * p * B / 2 / V)
    CY = poly(A['Cyb'], a) * beta + poly(A['Cydr'], a) * rud + poly(A['Cyda'], a) * ail
    Cm = poly(A['Cma'], a) + poly(A['Cmds'], a) * stab + poly(A['Cmq'], a) * C * q / 2 / V
    CLf = poly(A['CL'], a) * math.cos(2 * beta / 3) + poly(A['CLds'], a) * stab
    CDf = poly(A['CD'], a) * math.cos(beta) + A['CD0'] + poly(A['Cdds'], a) * stab
    CDf = max(CDf, CD0_FLOOR + K_FLOOR * CLf * CLf)
    qS = 0.5 * rho * V * V * S
    cb, sb, ca, sa = math.cos(beta), math.sin(beta), math.cos(alpha), math.sin(alpha)
    Dw, Yw, L = qS * (CDf * cb - CY * sb), qS * (CY * cb + CDf * sb), qS * CLf
    F = [-Dw * ca * cb - Yw * ca * sb + L * sa + T, -Dw * sb + Yw * cb, -Dw * sa * cb - Yw * sa * sb - L * ca]
    Mo = [qS * B * Cl, qS * C * Cm, qS * B * Cn]
    return F, Mo


def deriv(x, surf, T):
    """x = [u, v, w, p, q, r, phi, theta, psi, h] (body velocities, NASA frame)."""
    u, v, w, p, q, r, phi, th, psi, h = x
    V = math.sqrt(u * u + v * v + w * w)
    alpha, beta = math.atan2(w, u), math.asin(v / V)
    F, Mo = forces(V, beta, alpha, p, q, r, surf[1], surf[2], surf[0], isa_rho(h), T)
    g = [-G * math.sin(th), G * math.sin(phi) * math.cos(th), G * math.cos(phi) * math.cos(th)]
    ud = F[0] / M + g[0] - (q * w - r * v)
    vd = F[1] / M + g[1] - (r * u - p * w)
    wd = F[2] / M + g[2] - (p * v - q * u)
    L, Mm, N = Mo
    den = IXX * IZZ - IXZ ** 2
    pd = (IZZ * L + IXZ * N - (IXZ * (IYY - IXX - IZZ) * p + (IXZ ** 2 + IZZ * (IZZ - IYY)) * r) * q) / den
    qd = (Mm + (IZZ - IXX) * p * r + (r * r - p * p) * IXZ) / IYY
    rd = (IXZ * L + IXX * N + (IXZ * (IYY - IXX - IZZ) * r + (IXZ ** 2 + IXX * (IXX - IYY)) * p) * q) / den
    phid = p + (q * math.sin(phi) + r * math.cos(phi)) * math.tan(th)
    thd = q * math.cos(phi) - r * math.sin(phi)
    psid = (q * math.sin(phi) + r * math.cos(phi)) / math.cos(th)
    # climb rate from the body velocity
    hd = u * math.sin(th) - v * math.sin(phi) * math.cos(th) - w * math.cos(phi) * math.cos(th)
    return [ud, vd, wd, pd, qd, rd, phid, thd, psid, hd]


def rk4(x, surf, T, dt):
    k1 = deriv(x, surf, T)
    k2 = deriv([x[i] + 0.5 * dt * k1[i] for i in range(10)], surf, T)
    k3 = deriv([x[i] + 0.5 * dt * k2[i] for i in range(10)], surf, T)
    k4 = deriv([x[i] + dt * k3[i] for i in range(10)], surf, T)
    return [x[i] + dt / 6 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]) for i in range(10)]


def trim(V, h):
    """Level flight: alpha, stabilator, thrust with u-dot = w-dot = q-dot = 0 (Newton)."""
    z = [0.08, -0.03, 6000.0]
    for _ in range(60):
        def res(z):
            a, st, T = z
            x = [V * math.cos(a), 0, V * math.sin(a), 0, 0, 0, 0, a, 0, h]
            d = deriv(x, [st, 0, 0], T)
            return [d[0], d[2], d[4]]
        r0 = res(z)
        J = []
        for k, hh in enumerate([1e-6, 1e-6, 1e-2]):
            zz = list(z); zz[k] += hh
            rk = res(zz)
            J.append([(rk[i] - r0[i]) / hh for i in range(3)])
        Jm = [[J[c][rw] for c in range(3)] for rw in range(3)]
        dz = solve3(Jm, [-v for v in r0])
        z = [z[i] + dz[i] for i in range(3)]
        if max(abs(v) for v in dz) < 1e-12:
            break
    return z


def solve3(Am, b):
    m = [row[:] + [b[i]] for i, row in enumerate(Am)]
    for i in range(3):
        piv = max(range(i, 3), key=lambda k: abs(m[k][i]))
        m[i], m[piv] = m[piv], m[i]
        for k in range(i + 1, 3):
            f = m[k][i] / m[i][i]
            m[k] = [m[k][j] - f * m[i][j] for j in range(4)]
    out = [0.0] * 3
    for i in (2, 1, 0):
        out[i] = (m[i][3] - sum(m[i][j] * out[j] for j in range(i + 1, 3))) / m[i][i]
    return out


class Law:
    """FlightControlsL1, term for term. ctl = [stab, ail, rud, pitch_integral, n_filtered]."""

    def __init__(self, law=LAW):
        self.k = law

    def command_n(self, stick):
        k = self.k
        return 1 + stick * (k['n_max'] - 1) if stick >= 0 else 1 + stick * (1 - k['n_min'])

    def step(self, ctl, x, n, stick_pitch, stick_roll, pedals, dt):
        k = self.k
        u, v, w, p, q, r, phi, th, psi, h = x
        V = math.sqrt(u * u + v * v + w * w)
        alpha, beta = math.atan2(w, u), math.asin(v / V)
        a = min(max(alpha, AMIN), AMAX)
        qS = 0.5 * isa_rho(h) * V * V * S
        stab, ail, rud, ip, nf = ctl

        # pitch: G command, prefiltered; the lift curve inverted for the alpha that gives it here (plus a slow integral on
        # the G error), clamped at the AoA limit; the alpha error to a pitch-rate command about the steady pull-up rate.
        n_cmd = min(max(self.command_n(stick_pitch), k['n_min']), k['n_max'])
        nf_new = nf + (n_cmd - nf) * (1 - math.exp(-dt / k['n_tau']))
        en = nf_new - n
        ip_new = ip + k['ki_n'] * en * dt
        cl_req = nf_new * M * G / qS - poly(A['CLds'], a) * stab
        alpha_cmd = alpha_for_cl(cl_req / math.cos(2 * beta / 3)) + ip_new
        if alpha_cmd > k['alpha_limit']:
            alpha_cmd = k['alpha_limit']
            if en > 0:
                ip_new = ip                                # anti-windup at the AoA limit
        alpha_cmd = max(alpha_cmd, AMIN)
        # the flight path turns at g (n - cos theta cos phi) / V with the G the jet has now, so alpha-dot = q - that
        q_cmd = G * (n - math.cos(th) * math.cos(phi)) / V + k['k_alpha'] * (alpha_cmd - alpha)
        qd_des = k['wq'] * (q_cmd - q)
        m_req = IYY * qd_des - (IZZ - IXX) * p * r - (r * r - p * p) * IXZ
        stab_cmd = (m_req / (qS * C) - poly(A['Cma'], a) - poly(A['Cmq'], a) * C * q / 2 / V) / poly(A['Cmds'], a)
        lo, hi = ACT['stab'][2], ACT['stab'][3]
        if (stab_cmd < lo and en > 0) or (stab_cmd > hi and en < 0):
            ip_new = ip                                    # anti-windup: hold the integral while saturated

        # roll and yaw: roll-rate command; the coordinated-turn yaw rate plus sideslip feedback; L and N inverted together
        p_cmd = stick_roll * k['p_max']
        beta_cmd = -pedals * k['beta_per_pedal']
        r_cmd = G * math.sin(phi) * math.cos(th) / V + p * math.tan(a) + k['kbeta'] * (beta - beta_cmd)
        pd_des = k['wp'] * (p_cmd - p)
        rd_des = k['wr'] * (r_cmd - r)
        den = IXX * IZZ - IXZ ** 2
        cp = -(IXZ * (IYY - IXX - IZZ) * p + (IXZ ** 2 + IZZ * (IZZ - IYY)) * r) * q
        cr = (IXZ * (IYY - IXX - IZZ) * r + (IXZ ** 2 + IXX * (IXX - IYY)) * p) * q
        bl, bn = den * pd_des - cp, den * rd_des - cr            # [Izz Ixz; Ixz Ixx] [L; N] = (bl, bn)
        L_req = (IXX * bl - IXZ * bn) / den
        N_req = (IZZ * bn - IXZ * bl) / den
        qSb = qS * B
        cl_free = poly(A['Clb'], a) * beta + poly(A['Clp'], a) * p * B / 2 / V + poly(A['Clr'], a) * r * B / 2 / V
        cn_free = poly(A['Cnb'], a) * beta + poly(A['Cnp'], a) * p * B / 2 / V + poly(A['Cnr'], a) * r * B / 2 / V
        rl, rn = L_req / qSb - cl_free, N_req / qSb - cn_free
        la, lr, na, nr = poly(A['Clda'], a), poly(A['Cldr'], a), poly(A['Cnda'], a), poly(A['Cndr'], a)
        d = la * nr - lr * na
        ail_cmd = (rl * nr - lr * rn) / d
        rud_cmd = (la * rn - rl * na) / d

        out = [act(stab, stab_cmd, ACT['stab'], dt), act(ail, ail_cmd, ACT['ail'], dt), act(rud, rud_cmd, ACT['rud'], dt)]
        return out + [ip_new, nf_new]


def alpha_for_cl(cl):
    """The alpha below the lift peak whose basic C_L is cl (bisection on the fit's rising branch); clamped at its ends."""
    lo, hi = AMIN, 0.666                    # the cubic peaks at 0.666 rad (38.2 deg)
    if cl <= poly(A['CL'], lo):
        return lo
    if cl >= poly(A['CL'], hi):
        return hi
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if poly(A['CL'], mid) < cl:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def act(pos, cmd, spec, dt):
    bw, rate, lo, hi = spec
    target = pos + (cmd - pos) * (1 - math.exp(-bw * dt))
    step = min(max(target - pos, -rate * dt), rate * dt)
    return min(max(pos + step, lo), hi)


def load_factor(x, surf, T):
    u, v, w, p, q, r, phi, th, psi, h = x
    V = math.sqrt(u * u + v * v + w * w)
    F, _ = forces(V, math.asin(v / V), math.atan2(w, u), p, q, r, surf[1], surf[2], surf[0], isa_rho(h), T)
    return -F[2] / (M * G)


def fly(kt, h, seconds, inputs, law=LAW, dt=0.005):
    """inputs(t) -> (stick_pitch, stick_roll, pedals). Returns samples every 0.1 s."""
    V = kt * 1.68781
    a, st, T = trim(V, h)
    x = [V * math.cos(a), 0, V * math.sin(a), 0, 0, 0, 0, a, 0, h]
    ctl = [st, 0.0, 0.0, 0.0, 1.0]
    L = Law(law)
    out = []
    n = load_factor(x, ctl, T)
    steps = int(round(seconds / dt))
    for k in range(steps + 1):
        t = k * dt
        if k % 20 == 0:
            u, v, w, p, q, r, phi, th, psi, hh = x
            Vn = math.sqrt(u * u + v * v + w * w)
            out.append(dict(t=round(t, 3), n=n, alpha=math.atan2(w, u) / D2R, beta=math.asin(v / Vn) / D2R, p=p / D2R,
                            q=q / D2R, r=r / D2R, phi=phi / D2R, theta=th / D2R, kt=Vn / 1.68781, h=hh,
                            stab=ctl[0] / D2R, ail=ctl[1] / D2R, rud=ctl[2] / D2R))
        if k == steps:
            break
        sp, sr, pe = inputs(t)
        ctl = L.step(ctl, x, n, sp, sr, pe, dt)
        x = rk4(x, ctl, T, dt)
        n = load_factor(x, ctl, T)
        if not all(math.isfinite(c) for c in x):
            break
    return out, (a, st, T)


def stick_for(n_cmd, law=LAW):
    return (n_cmd - 1) / (law['n_max'] - 1) if n_cmd >= 1 else (n_cmd - 1) / (1 - law['n_min'])


def report(law=LAW, verbose=True):
    res = {}
    # T2: 4 g step at 350 kt, 15,000 ft
    s, _ = fly(350, 15000, 4.0, lambda t: (stick_for(4.0, law) if t >= 0.5 else 0.0, 0, 0), law)
    ns = [(o['t'], o['n']) for o in s]
    t90 = next((t for t, n in ns if t >= 0.5 and n >= 1 + 0.9 * 3), None)
    peak = max(n for t, n in ns)
    res['g_step'] = dict(t90=None if t90 is None else round(t90 - 0.5, 2), peak=round(peak, 3), at3s=round(ns[-6][1], 3))
    # T1: hands off 10 s
    s, _ = fly(350, 15000, 10.0, lambda t: (0, 0, 0), law)
    res['hands_off'] = dict(dn=round(max(abs(o['n'] - 1) for o in s), 4), dh=round(s[-1]['h'] - 15000, 1))
    # T3: full aft at 500 kt, 10,000 ft
    s, _ = fly(500, 10000, 3.0, lambda t: (1.0 if t >= 0.2 else 0, 0, 0), law)
    res['g_limit'] = dict(peak=round(max(o['n'] for o in s), 3), last=round(s[-1]['n'], 3))
    # T4: full aft at 160 kt, 10,000 ft
    s, _ = fly(160, 10000, 6.0, lambda t: (1.0 if t >= 0.2 else 0, 0, 0), law)
    res['aoa_limit'] = dict(peak_alpha=round(max(o['alpha'] for o in s), 2), beta=round(max(abs(o['beta']) for o in s), 2),
                            p=round(max(abs(o['p']) for o in s), 2), last_alpha=round(s[-1]['alpha'], 2))
    # T5: roll, half and full stick, 350 kt
    for frac in (0.5, 1.0):
        s, _ = fly(350, 15000, 2.0, lambda t, f=frac: (0, f if t >= 0.2 else 0, 0), law)
        cmd = frac * law['p_max'] / D2R
        late = [o['p'] for o in s if o['t'] >= 1.0 and abs(o['phi']) < 170]
        res['roll_%.1f' % frac] = dict(cmd=round(cmd, 1), p_at_1s=round(late[0] if late else float('nan'), 1),
                                       t90=next((o['t'] - 0.2 for o in s if o['p'] >= 0.9 * cmd), None))
    # T6: rudder pulse, Dutch roll decay
    s, _ = fly(350, 15000, 8.0, lambda t: (0, 0, 0.5 if 0.2 <= t < 0.7 else 0), law)
    b = [(o['t'], o['beta']) for o in s]
    peak = max(abs(v) for t, v in b if t <= 2.0)
    late = max(abs(v) for t, v in b if t >= 4.7)
    res['dutch_roll'] = dict(beta_peak=round(peak, 3), beta_after_4s=round(late, 3), ratio=round(late / peak, 3))
    # T7: roll into a 60 deg bank and hold 2 g
    def turn(t):
        if t < 0.2:
            return (0, 0, 0)
        return (stick_for(2.0, law) if t >= 0.6 else 0, 0.5 if t < 0.2 + 60 / 75.0 else 0, 0)
    s, _ = fly(350, 15000, 5.0, turn, law)
    res['turn_entry'] = dict(max_beta=round(max(abs(o['beta']) for o in s), 3), bank_end=round(s[-1]['phi'], 1))
    if verbose:
        for k, v in res.items():
            print(k, v)
    return res


if __name__ == '__main__':
    for kt, h in [(350, 15000), (500, 10000), (160, 10000)]:
        a, st, T = trim(kt * 1.68781, h)
        print('trim %d kt %d ft: alpha %.3f deg, stab %.3f deg, thrust %.0f lbf' % (kt, h, a / D2R, st / D2R, T))
    report()

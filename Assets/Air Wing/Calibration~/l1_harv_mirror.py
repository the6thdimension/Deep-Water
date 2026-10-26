"""Air Wing L1 mirror (METHODOLOGY C17): the published F/A-18 HARV six-degree-of-freedom model, dependency-free.

Source (W32, re-decided by SIX 2026-10-26). PUBLIC:
  Chakraborty, Seiler and Balas, "Applications of linear analysis tools to F/A-18 flight control laws", J. Guidance,
  Control, and Dynamics 34(1), 2011, Appendix Tables A1-A2: least-squares polynomial fits in angle of attack to NASA
  F/A-18 HARV flight-test data. Cross-checked number for number against the authors' public MATLAB files
  (F18Analysis.zip: f18full.m, f18_data.m, F18AeroData.mat; University of Minnesota, AerospaceControl/V&VWebpage).
  Geometry and inertias: Buttrill, Arbuckle and Hoffler, NASA TM-107601 (1992), Table 3.5, first row.
  Valid for alpha 0-60 deg. Sideslip enters linearly (no sideslip data). No flaps, speed brake or gear terms.

FINDING (2026-10-26): f18full.m's betad adds the thrust term T cos(alpha) sin(beta)/(mV) where body kinematics (and
Stevens and Lewis) subtract it. The port keeps the authors' line as published; betad_kinematic() below derives betad
from the body-frame equations and is what Air Wing follows. The two differ by exactly 2 T cos(alpha) sin(beta)/(mV);
Vd and alphad agree to every printed digit. Trim and the doublet (beta = 0) are untouched by it.

This file is a line-for-line port of f18full.m (the non-polynomial trig branch), in the paper's units (ft, slug, lbf,
rad). It prints the reference numbers the C# tests (AirframeL1Tests) reproduce:
  1. the six coefficients at reference points;
  2. the state derivatives (Vd, betad, alphad, pd, qd, rd) at a trim point and at a skewed state;
  3. a 3 s open-loop stabilator doublet from trim, integrated with RK4 at 1 ms, with the paper's g (32.2 ft/s^2)
     and again with standard gravity (9.80665 m/s^2 = 32.17405 ft/s^2), which the C# core uses.

Run:  python -I l1_harv_mirror.py
"""
import math

# --- f18_data.m -------------------------------------------------------------------------------------------
S, B, C = 400.0, 37.42, 11.52                     # ft^2, ft, ft
RHO = 1.0660e-3                                   # slug/ft^3 (25,000 ft in f18_data.m)
IXX, IYY, IZZ, IXZ = 23000.0, 151293.0, 169945.0, -2971.0   # slug ft^2 (TM-107601 Table 3.5)
M = 1034.5                                        # slug
G = 32.2                                          # ft/s^2 (f18_data.m)

A = dict(  # F18AeroData.mat, to the digits the paper prints (Tables A1-A2); the .mat agrees to 4 places
    Clb=[-0.0556, -0.4153, -0.3620, 2.3843, -1.6196], Cldr=[0.0129, 0.0014, 0.0083, -0.0274],
    Clda=[0.1424, -0.0516, -0.2646, 0.1989], Clp=[-0.3540, 0.2377], Clr=[0.1983, 0.7804, -1.0871],
    Cnb=[0.0885, 0.0329, -0.3816], Cndr=[-0.0780, -0.0176, 0.5564, -0.8980, 0.3899],
    Cnda=[0.0104, 0.0584, -0.3413, 0.2694], Cnr=[-0.4326, -0.1307], Cnp=[0.0792, -0.0881],
    Cyb=[-0.7344, 0.2654, -0.1926], Cyda=[-0.1656, -0.2403, 1.5317, -0.8500], Cydr=[0.2054, 0.4082, -1.6921, 0.9351],
    Cma=[-0.0866, 0.5110, -1.2897], Cmds=[-0.9051, -0.3245, 0.9338], Cmq=[-4.1186, 10.9921, -68.5641, 64.7190],
    CLds=[0.5725, 0.4055, -2.6975, 2.1852], Cdds=[0.0366, -0.2739, 4.2360, -3.8578],
    CL=[-0.0204, 5.677, -5.4246, 1.1645], CD=[-1.4994, -0.1995, 6.3971, -5.7341, 1.4610], CD0=1.5036,
)


def poly(c, a):
    return sum(ci * a ** i for i, ci in enumerate(c))


def coefficients(V, beta, alpha, p, q, r, ail, rud, stab):
    """The six coefficients of f18full.m: C_l, C_m, C_n (body moments), C_Y, C_lift, C_drag."""
    Cl = (poly(A['Clb'], alpha) * beta + poly(A['Clda'], alpha) * ail + poly(A['Cldr'], alpha) * rud
          + poly(A['Clp'], alpha) * p * B / 2 / V + poly(A['Clr'], alpha) * r * B / 2 / V)
    Cn = (poly(A['Cnb'], alpha) * beta + poly(A['Cnda'], alpha) * ail + poly(A['Cndr'], alpha) * rud
          + poly(A['Cnr'], alpha) * r * B / 2 / V + poly(A['Cnp'], alpha) * p * B / 2 / V)
    CY = poly(A['Cyb'], alpha) * beta + poly(A['Cydr'], alpha) * rud + poly(A['Cyda'], alpha) * ail
    Cm = poly(A['Cma'], alpha) + poly(A['Cmds'], alpha) * stab + poly(A['Cmq'], alpha) * C * q / 2 / V
    CLift = poly(A['CL'], alpha) * math.cos(2 * beta / 3) + poly(A['CLds'], alpha) * stab
    CDrag = poly(A['CD'], alpha) * math.cos(beta) + A['CD0'] + poly(A['Cdds'], alpha) * stab
    return Cl, Cm, Cn, CY, CLift, CDrag


def f18full(x, u):
    V, beta, alpha, p, q, r, phi, theta, psi = x
    ail, rud, stab, T = u
    Cl, Cm, Cn, CY, CLift, CDrag = coefficients(V, beta, alpha, p, q, r, ail, rud, stab)
    qbar = 0.5 * RHO * V * V
    L, Mm, N = Cl * qbar * S * B, Cm * qbar * S * C, Cn * qbar * S * B
    Lift = CLift * qbar * S
    cb, sb, ca, sa = math.cos(beta), math.sin(beta), math.cos(alpha), math.sin(alpha)
    cph, sph, cth, sth = math.cos(phi), math.sin(phi), math.cos(theta), math.sin(theta)
    CD_w = CDrag * cb - CY * sb
    CY_w = CY * cb + CDrag * sb
    Vd = (-qbar * S * CD_w / M + G * (cph * cth * sa * cb + sph * cth * sb - sth * ca * cb) + (T / M) * cb * ca)
    betad = (qbar * S * CY_w / M / V + p * sa - r * ca + G * cth * sph * cb / V
             + sb * (G * ca * sth - G * sa * cph * cth + T * ca / M) / V)
    alphad = (-Lift / cb / M / V + q - math.tan(beta) * (p * ca + r * sa)
              + G * (cph * cth * ca + sth * sa) / cb / V - (T / cb / M / V) * sa)
    den = IXX * IZZ - IXZ ** 2
    pd = (IZZ * L + IXZ * N - (IXZ * (IYY - IXX - IZZ) * p + (IXZ ** 2 + IZZ * (IZZ - IYY)) * r) * q) / den
    qd = (Mm + (IZZ - IXX) * p * r + (r * r - p * p) * IXZ) / IYY
    rd = (IXZ * L + IXX * N + (IXZ * (IYY - IXX - IZZ) * r + (IXZ ** 2 + IXX * (IXX - IYY)) * p) * q) / den
    phid = p + (q * sph + r * cph) * math.tan(theta)
    thetad = q * cph - r * sph
    psid = (q * sph + r * cph) / cth
    return [Vd, betad, alphad, pd, qd, rd, phid, thetad, psid]


def betad_kinematic(x, u):
    """betad from the body-frame equations (u, v, w), independent of f18full.m's closed form."""
    V, beta, alpha, p, q, r, phi, theta, psi = x
    ail, rud, stab, T = u
    Cl, Cm, Cn, CY, CLift, CDrag = coefficients(V, beta, alpha, p, q, r, ail, rud, stab)
    qS = 0.5 * RHO * V * V * S
    cb, sb, ca, sa = math.cos(beta), math.sin(beta), math.cos(alpha), math.sin(alpha)
    Dw, Yw, L = qS * (CDrag * cb - CY * sb), qS * (CY * cb + CDrag * sb), qS * CLift
    F = [-Dw * ca * cb - Yw * ca * sb + L * sa + T, -Dw * sb + Yw * cb, -Dw * sa * cb - Yw * sa * sb - L * ca]
    uvw = [V * ca * cb, V * sb, V * sa * cb]
    g = [-G * math.sin(theta), G * math.sin(phi) * math.cos(theta), G * math.cos(phi) * math.cos(theta)]
    cr = [q * uvw[2] - r * uvw[1], r * uvw[0] - p * uvw[2], p * uvw[1] - q * uvw[0]]
    vd = [F[i] / M + g[i] - cr[i] for i in range(3)]
    Vd = sum(uvw[i] * vd[i] for i in range(3)) / V
    return (V * vd[1] - uvw[1] * Vd) / (V * V * cb)


def trim(V):
    """Level, wings-level flight at V: solve alpha, stabilator, thrust for Vd = alphad = qd = 0 (Newton)."""
    z = [0.1, -0.05, 5000.0]
    for _ in range(50):
        def res(z):
            a, st, T = z
            d = f18full([V, 0, a, 0, 0, 0, 0, a, 0], [0, 0, st, T])
            return [d[0], d[2], d[4]]
        r0 = res(z)
        J = []
        for k, h in enumerate([1e-6, 1e-6, 1e-2]):
            zz = list(z); zz[k] += h
            rk = res(zz)
            J.append([(rk[i] - r0[i]) / h for i in range(3)])
        # solve J^T dz = -r0 (J stored by column)
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
    x = [0.0] * 3
    for i in (2, 1, 0):
        x[i] = (m[i][3] - sum(m[i][j] * x[j] for j in range(i + 1, 3))) / m[i][i]
    return x


def rk4(x, u_of_t, t_end, dt=1e-3):
    """RK4 with the inputs held over each step. The input schedule gets the time from the step count (k * dt), never
    from an accumulated clock, so a switch at 0.5 s falls on a step boundary exactly (a float clock can hold a 4 deg
    stabilator step one step too long: slice 2a found 0.03 deg of alpha that way)."""
    t, out = 0.0, []
    n = int(round(t_end / dt))
    for k in range(n + 1):
        t = k * dt
        if k % 100 == 0:
            out.append((round(t, 6), list(x)))
        if k == n:
            break
        u = u_of_t(round(k * dt, 9))
        k1 = f18full(x, u)
        k2 = f18full([x[i] + 0.5 * dt * k1[i] for i in range(9)], u)
        k3 = f18full([x[i] + 0.5 * dt * k2[i] for i in range(9)], u)
        k4 = f18full([x[i] + dt * k3[i] for i in range(9)], u)
        x = [x[i] + dt / 6 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]) for i in range(9)]
    return out


if __name__ == '__main__':
    d2r = math.pi / 180
    print('# 1. coefficients (C_l, C_m, C_n, C_Y, C_lift, C_drag)')
    for args in [(500, 0, 0, 0, 0, 0, 0, 0, 0), (500, 3 * d2r, 10 * d2r, 0.1, 0.05, -0.05, 5 * d2r, -4 * d2r, -3 * d2r),
                 (300, -5 * d2r, 38 * d2r, -0.2, 0.1, 0.1, -10 * d2r, 10 * d2r, 5 * d2r)]:
        print('coef', args, ['%.9f' % c for c in coefficients(*args)])

    V = 500.0
    a, st, T = trim(V)
    print('# 2. trim at V = 500 ft/s, rho = 1.066e-3: alpha %.9f rad (%.4f deg), stab %.9f rad (%.4f deg), thrust %.6f lbf'
          % (a, a / d2r, st, st / d2r, T))
    print('derivs trim', ['%.9e' % v for v in f18full([V, 0, a, 0, 0, 0, 0, a, 0], [0, 0, st, T])[:6]])
    skew_x = [V, 3 * d2r, 10 * d2r, 0.1, 0.05, -0.05, 20 * d2r, 5 * d2r, 0.0]
    skew_u = [5 * d2r, -4 * d2r, -3 * d2r, 9000.0]
    print('derivs skew x', skew_x, 'u', skew_u)
    print('derivs skew', ['%.9e' % v for v in f18full(skew_x, skew_u)[:6]])
    print('betad skew, body kinematics (Air Wing follows this): %.9e' % betad_kinematic(skew_x, skew_u))

    print('# 3. stabilator doublet from trim: -2 deg for 0.5 s, then +2 deg for 0.5 s, then trim; thrust held')
    def u_of_t(t):
        ds = -2 * d2r if t < 0.5 else (2 * d2r if t < 1.0 else 0.0)
        return [0, 0, st + ds, T]
    for t, x in rk4([V, 0, a, 0, 0, 0, 0, a, 0], u_of_t, 3.0):
        if abs(t * 2 - round(t * 2)) < 1e-9:
            print('doublet t %.1f V %.6f alpha %.9f q %.9f theta %.9f' % (t, x[0], x[2], x[4], x[7]))

    G = 9.80665 / 0.3048
    a, st, T = trim(V)
    print('# 3b. the same with standard gravity: trim alpha %.9f stab %.9f thrust %.6f' % (a, st, T))
    for t, x in rk4([V, 0, a, 0, 0, 0, 0, a, 0], u_of_t, 3.0):
        if abs(t * 2 - round(t * 2)) < 1e-9:
            print('doublet-std t %.1f V %.6f alpha %.9f q %.9f theta %.9f' % (t, x[0], x[2], x[4], x[7]))

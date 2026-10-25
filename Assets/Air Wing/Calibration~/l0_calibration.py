"""Air Wing L0 calibration mirror (METHODOLOGY C17): the same equations as the C# L0 parts, dependency-free.

One free number is fitted to one public figure (C16):
  CALIBRATED  thrust lapse exponent x in T = T_SL * sigma^x  ->  service ceiling 50,000 ft (Navy fact file, "50,000+ feet").
  VALIDATION  maximum level speed (Navy fact file "Mach 1.8+"; Boeing "Mach 1.6"): reported, not fitted.

Service ceiling: the altitude where the best steady rate of climb is 100 ft/min (0.508 m/s), 1 g, clean, maximum
afterburner, at the test weight. The climb-speed search is capped at Mach 0.9 because L0 has no transonic drag rise
(BLUEPRINT §4: Mach effects are L2); above that cap a Mach-blind polar would climb unrealistically well.

Run:  python -I l0_calibration.py
The C# tests (AirframeL0Tests) must reproduce the printed numbers.
"""
import math

# --- ISA (U.S. Standard Atmosphere 1976, geopotential altitude) -------------------------------------------
G0, R, T0, P0, GAMMA = 9.80665, 287.05287, 288.15, 101325.0, 1.4
RHO0 = P0 / (R * T0)


def isa(h):
    if h <= 11000.0:
        t = T0 - 0.0065 * h
        p = P0 * (t / T0) ** (G0 / (R * 0.0065))
    elif h <= 20000.0:
        t = 216.65
        p11 = P0 * (216.65 / T0) ** (G0 / (R * 0.0065))
        p = p11 * math.exp(-G0 * (h - 11000.0) / (R * t))
    else:
        t = 216.65 + 0.001 * (h - 20000.0)
        p11 = P0 * (216.65 / T0) ** (G0 / (R * 0.0065))
        p20 = p11 * math.exp(-G0 * 9000.0 / (R * 216.65))
        p = p20 * (t / 216.65) ** (-G0 / (R * 0.001))
    return p / (R * t), math.sqrt(GAMMA * R * t)  # density, speed of sound


# --- The F/A-18E L0 profile (values and their sources: Calibration~/F18E-SOURCES.md) ----------------------
LB, LBF, FT = 0.45359237, 4.4482216152605, 0.3048
S = 500.0 * FT * FT                    # AUTHORED (widely quoted; no official source found)
SPAN = 44.9 * FT                       # Navy fact file
AR = SPAN * SPAN / S
E_OSWALD = 0.80                        # AUTHORED
K = 1.0 / (math.pi * E_OSWALD * AR)
CD0 = 0.025                            # AUTHORED
T_MAX_SL = 2 * 22000.0 * LBF           # Navy fact file / GE datasheet: 22,000 lb static per engine
MASS = (32081.0 + 0.5 * 14400.0) * LB  # AUTHORED test weight: empty + half internal fuel
W = MASS * G0
CEILING = 50000.0 * FT
RC_SERVICE = 100.0 * FT / 60.0


def drag(v, rho, n=1.0):
    q_s = 0.5 * rho * v * v * S
    cl = n * W / q_s
    return q_s * (CD0 + K * cl * cl)


def best_climb(h, x, mach_cap=0.9):
    rho, a = isa(h)
    t = T_MAX_SL * (rho / RHO0) ** x
    best = -1e9
    for i in range(1, 401):
        v = mach_cap * a * i / 400.0
        best = max(best, (t - drag(v, rho)) * v / W)
    return best


def service_ceiling(x):
    lo, hi = 0.0, 30000.0
    for _ in range(60):
        mid = 0.5 * (lo + hi)
        if best_climb(mid, x) > RC_SERVICE:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def fit_lapse():
    lo, hi = 0.3, 2.5   # a steeper lapse gives a lower ceiling
    for _ in range(60):
        mid = 0.5 * (lo + hi)
        if service_ceiling(mid) > CEILING:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def max_level_mach(h, x):
    rho, a = isa(h)
    t = T_MAX_SL * (rho / RHO0) ** x
    v_lo, v_hi = 50.0, 2000.0   # Mach-blind: thrust minus drag falls through zero once
    for _ in range(80):
        mid = 0.5 * (v_lo + v_hi)
        if t - drag(mid, rho) > 0:
            v_lo = mid
        else:
            v_hi = mid
    return 0.5 * (v_lo + v_hi) / a


if __name__ == "__main__":
    x = fit_lapse()
    print(f"aspect ratio {AR:.4f}, induced factor K {K:.5f}, mass {MASS:.1f} kg")
    print(f"CALIBRATED thrust lapse exponent x = {x:.4f}  ->  service ceiling {service_ceiling(x) / FT:,.0f} ft")
    for h_ft in (0, 10000, 20000, 36089, 40000):
        print(f"VALIDATION max level speed at {h_ft:>6,} ft: Mach {max_level_mach(h_ft * FT, x):.2f}  (public: Mach 1.8+ Navy, 1.6 Boeing)")

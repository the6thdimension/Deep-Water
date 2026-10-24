"""Hydrostatic predictions for a box hull on Sea State probe columns.

usage: python -I hull_predict.py --length L --beam B --height H --draft d --across N
                                 [--com-y Y] [--inertia-roll I] [--heel DEG] [--rho 1025] [--g 9.81]

  --across N        probe columns across the beam (BuoyancyHullSO.FillBoxGrid's nx)
  --com-y Y         centre of mass relative to the box centre, m (negative = lower, ballast)
  --inertia-roll I  roll inertia about the centre of mass as Unity REPORTS it (rb.inertiaTensor.z
                    for a hull whose length runs along z). Default: Unity's automatic tensor for a
                    uniform box collider, shifted to the centre of mass (parallel axis), which is
                    what Unity does when a script sets Rigidbody.centerOfMass.

Prints KB, BM (continuous and for the N-column probe waterplane), KG, GM, the righting moment at
the given heel (wall-sided formula), the undamped roll period and the IMO weather-criterion period.
The probe model's numbers (discrete BM) are what Sea State should reproduce; the continuous ones are
what the real box would do.
"""
import argparse, math


def main():
    ap = argparse.ArgumentParser()
    for k in ('length', 'beam', 'height', 'draft'):
        ap.add_argument('--' + k, type=float, required=True)
    ap.add_argument('--across', type=int, required=True)
    ap.add_argument('--com-y', type=float, default=0.0)
    ap.add_argument('--inertia-roll', type=float, default=None)
    ap.add_argument('--heel', type=float, default=5.0)
    ap.add_argument('--rho', type=float, default=1025.0)
    ap.add_argument('--g', type=float, default=9.81)
    a = ap.parse_args()

    L, B, H, d, n = a.length, a.beam, a.height, a.draft, a.across
    m = a.rho * L * B * d
    kb = d / 2
    bm = B * B / (12 * d)
    bm_n = bm * (n * n - 1) / (n * n)          # probe waterplane: sum x^2 over n columns
    kg = H / 2 + a.com_y
    gm, gm_n = kb + bm - kg, kb + bm_n - kg
    phi = math.radians(a.heel)
    gz = math.sin(phi) * (gm_n + 0.5 * bm * math.tan(phi) ** 2)
    i_roll = a.inertia_roll if a.inertia_roll else m * (B * B + H * H) / 12 + m * a.com_y ** 2
    c = 0.373 + 0.023 * (B / d) - 0.043 * (L / 100)

    def period(g_m):
        return 2 * math.pi * math.sqrt(i_roll / (m * a.g * g_m)) if g_m > 0 else float('nan')

    print(f"mass {m:,.0f} kg (seawater at the design draft); density ratio d/H = {d / H:.3f}")
    print(f"KB {kb:.3f} m   BM {bm:.3f} m (probe waterplane, {n} across: {bm_n:.3f} m)   KG {kg:.3f} m")
    print(f"GM {gm:.3f} m continuous   GM {gm_n:.3f} m probe model"
          + ("   ** NEGATIVE: unstable upright; it will lie over (ballast it: lower --com-y) **" if gm_n <= 0 else ""))
    print(f"righting moment at {a.heel:g} deg: {m * a.g * gz:,.0f} N·m (wall-sided, probe GM)")
    print(f"roll inertia about the centre of mass: {i_roll:,.1f} kg·m²"
          + ("" if a.inertia_roll else " (Unity automatic: uniform box, parallel-axis shifted)"))
    print(f"undamped roll period: {period(gm_n):.2f} s probe model, {period(gm):.2f} s continuous")
    if gm > 0:
        print(f"IMO weather-criterion period T = 2CB/sqrt(GM): {2 * c * B / math.sqrt(gm):.2f} s "
              f"(C = {c:.3f}; real ships, includes added inertia; a yardstick, not an exact answer)")
    if gm_n > 0:
        print(f"column-centre defect check: forces at column centres would give GM = BM_n + (KB_col - KG) "
              f"= {bm_n - a.com_y:.3f} m -> period {period(bm_n - a.com_y):.2f} s")


if __name__ == '__main__':
    main()

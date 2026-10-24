"""Borrow a sister hull's public model-basin data for a hull that has none, and say how far to trust it.

usage:
  python -I -X utf8 scale_sister.py --sister L,B,T,Cb --target L,B,T,Cb [--sister-name NAME]
                                    [--sister-screws N --target-screws M]
                                    [--ct "Fn:CT,Fn:CT,..." --model-length LM [--form-factor K]]

  L, B, T  length, waterline beam and draft, m; use the SAME length definition for both hulls (Lpp where
           the sister's coefficients are on Lpp).  Cb  block coefficient (V / (L B T)) on that length.
  --sister-screws / --target-screws  number of propellers. A different layout means only the hull terms
           (and resistance) transfer: propeller, rudder and interaction terms must come from elsewhere.
  --ct     the sister's model-basin total resistance coefficients at several Froude numbers, measured on a
           model LM metres long (fresh water, 15 C). They are carried to the target at full scale by ITTC-57:
           C_R = C_T,model - (1+k) C_F(Re_model) is taken as equal at equal Fn, then
           C_T,target = C_R + (1+k) C_F(Re_target).

What transfers and how:
  - Non-dimensional manoeuvring derivatives (prime system: forces over 1/2 rho L T U^2, moments over 1/2 rho L^2 T U^2;
    or 1/2 rho L^2 U^2 if that is how the source writes them) carry over unchanged at equal Froude number.
    Label them DERIVED with the sister's name and source.
  - Speeds map at equal Froude number: V_target = V_sister * sqrt(L_target / L_sister).
  - Mass, inertia, propeller and rudder sizes do NOT transfer: they come from the target's own geometry.

The verdict compares form ratios (L/B, B/T, Cb). Within 10 % on all three: close, use the data. Within 20 %:
usable with care; check the result against any full-scale figure the target has (top speed, a turning circle).
Beyond that: too far; find a closer sister or ask (the skill's 'no data' rule).
"""
import math
import sys

NU_SEA_15 = 1.1883e-6     # m^2/s, sea water at 15 C (ITTC 2011)
NU_FRESH_15 = 1.1386e-6   # m^2/s, fresh water at 15 C
G = 9.81


def parse4(s):
    v = [float(x) for x in s.split(",")]
    if len(v) != 4:
        raise SystemExit(f"expected L,B,T,Cb, got '{s}'")
    return dict(zip(("L", "B", "T", "Cb"), v))


def cf(re):
    """ITTC-57 friction line."""
    return 0.075 / (math.log10(re) - 2) ** 2


def arg(name, default=None):
    a = sys.argv
    return a[a.index(name) + 1] if name in a else default


def main():
    if "--sister" not in sys.argv or "--target" not in sys.argv:
        print(__doc__); sys.exit(2)
    s, t = parse4(arg("--sister")), parse4(arg("--target"))
    name = arg("--sister-name", "sister")

    print(f"== {name} -> target")
    worst = 0.0
    for label, f in (("L/B", lambda h: h["L"] / h["B"]), ("B/T", lambda h: h["B"] / h["T"]), ("Cb", lambda h: h["Cb"])):
        a, b = f(s), f(t)
        d = (b - a) / a
        worst = max(worst, abs(d))
        print(f"   {label:4} sister {a:7.3f}  target {b:7.3f}  ({d * 100:+.1f} %)")
    verdict = "CLOSE: use the data" if worst <= 0.10 else ("USABLE WITH CARE: check against a full-scale figure"
                                                          if worst <= 0.20 else "TOO FAR: find a closer sister or ask")
    print(f"   verdict: {verdict} (worst form-ratio difference {worst * 100:.1f} %)")
    ss, ts = arg("--sister-screws"), arg("--target-screws")
    if ss and ts and ss != ts:
        print(f"   PROPULSION DIFFERS: sister {ss} screw(s), target {ts}. Only hull derivatives and resistance transfer;"
              " take propeller, rudder and interaction terms from a hull with the target's layout")
    elif not (ss and ts):
        print("   propulsion layout not compared (pass --sister-screws and --target-screws)")

    lam = t["L"] / s["L"]
    print(f"   scale lambda = {lam:.4f}; speeds at equal Fn: V_target = {math.sqrt(lam):.4f} x V_sister")
    disp_geo = s["Cb"] * s["L"] * s["B"] * s["T"] * lam ** 3
    disp_t = t["Cb"] * t["L"] * t["B"] * t["T"]
    print(f"   displaced volume: target's own {disp_t:.0f} m3 (geometric scaling would give {disp_geo:.0f} m3; use the target's)")

    ct_arg = arg("--ct")
    if ct_arg:
        lm = float(arg("--model-length", "0") or 0)
        if lm <= 0:
            raise SystemExit("--ct needs --model-length (the sister's model length, m)")
        k = float(arg("--form-factor", "0.15"))
        print(f"   resistance (ITTC-57, 1+k = {1 + k:.3f}, model {lm} m fresh water, target {t['L']} m sea water):")
        print("     Fn      V_target kn   CT_model     CR          CT_target")
        for pair in ct_arg.split(","):
            fn, ct = (float(x) for x in pair.split(":"))
            v_m = fn * math.sqrt(G * lm)
            # At low Fn the friction line can exceed the measured total; C_R is then taken as 0, not negative.
            cr = max(ct - (1 + k) * cf(v_m * lm / NU_FRESH_15), 0.0)
            v_t = fn * math.sqrt(G * t["L"])
            ct_t = cr + (1 + k) * cf(v_t * t["L"] / NU_SEA_15)
            print(f"     {fn:.3f}   {v_t / 0.514444:9.2f}   {ct:.4e}   {cr:.4e}   {ct_t:.4e}")
        print("     (add a correlation allowance and appendage factor at full scale; calibrate the appendage factor on top speed)")
    print(f"   label every borrowed number: DERIVED from {name}, scaled by this script; state the verdict in the provenance")


if __name__ == "__main__":
    main()

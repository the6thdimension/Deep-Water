"""Check a ship layout JSON (the contract between the Blender authoring script and the Unity hull builder).

usage: python -I -X utf8 check_layout.py <Ship_Layout.json> [--rho 1025]

Prints PASS / WARN / FAIL lines and exits 1 on any FAIL. It checks that:
  - the contract's fields are all present;
  - mass and displacement agree (m = rho V);
  - KG = KB + BM - GM;
  - the probe grid Sea State floats sits close to the converged hull hydrostatics (METHODOLOGY AP16);
  - form ratios and the roll gyradius fall in the ranges real displacement ships occupy;
  - the roll period is plausible (undamped 2 pi k / sqrt(g GM), and the IMO 2008 IS Code estimate).
It also lists every value the layout marks AUTHORED, so they can be reported. A range check that WARNs is
not wrong by itself: a catamaran or a planing craft sits outside them on purpose; say so in the provenance.
"""
import json
import math
import sys

G = 9.81
REQUIRED_HYDRO = ["waterline_y", "displacement_t", "mass_kg", "keel_y", "draft_m", "lcb_z", "kb_m", "bm_m",
                  "waterplane_m2", "beam_wl_m", "length_wl_m", "gm_m", "kg_m", "centre_of_mass", "gyradius_m",
                  "gm_note", "gyradius_note"]
# Typical displacement ships (frigates to carriers, auxiliaries); outside these, say why in the provenance.
RANGES = {"L/B": (3.0, 11.0), "B/T": (2.0, 5.0), "Cb": (0.35, 0.85), "k_roll/B": (0.30, 0.45), "GM/B": (0.02, 0.15)}

fails = 0


def say(level, msg):
    global fails
    if level == "FAIL":
        fails += 1
    print(f"{level:4}  {msg}")


def near(name, a, b, tol, level="FAIL"):
    rel = abs(a - b) / max(abs(b), 1e-9)
    say("PASS" if rel <= tol else level, f"{name}: {a:.4g} vs {b:.4g} ({rel * 100:.2f} %, tolerance {tol * 100:.1f} %)")


def in_range(name, v):
    lo, hi = RANGES[name]
    say("PASS" if lo <= v <= hi else "WARN", f"{name} = {v:.3f} (typical {lo}-{hi})")


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__); sys.exit(2)
    rho = float(args[args.index("--rho") + 1]) if "--rho" in args else 1025.0
    lay = json.load(open(args[0], encoding="utf-8"))
    print(f"== {args[0]}\n   source: {lay.get('source', '?')}\n   frame:  {lay.get('frame', '?')}")

    h = lay.get("hydrostatics", {})
    missing = [k for k in REQUIRED_HYDRO if k not in h]
    say("FAIL" if missing else "PASS", f"hydrostatics fields{': missing ' + ', '.join(missing) if missing else ' complete'}")
    for k in ("probes", "compartments", "hydrostatics_note", "compartments_note"):
        say("PASS" if k in lay else "FAIL", f"'{k}' present")
    if missing:
        sys.exit(1)

    m, disp = h["mass_kg"], h["displacement_t"]
    near("mass = displacement", m / 1000.0, disp, 0.002)
    vol = m / rho
    near("KG = KB + BM - GM", h["kg_m"], h["kb_m"] + h["bm_m"] - h["gm_m"], 0.01)
    say("PASS" if h["gm_m"] > 0 else "FAIL", f"GM {h['gm_m']} m is positive (an upright ship is stable)")

    p = lay["probes"]
    n = len(p.get("columns", []))
    say("PASS" if n > 0 else "FAIL", f"probe columns: {n} on a {p.get('grid', '?')} grid")
    if n:
        near("probe displacement vs hull", p["displacement_t"], disp, 0.01, "WARN")
        near("probe BM vs hull (heel behaviour, AP16)", p["bm_m"], h["bm_m"], 0.05, "WARN")
        lcb_off = abs(p["lcb_z"] - h["lcb_z"])
        say("PASS" if lcb_off <= 0.005 * h["length_wl_m"] else "WARN",
            f"probe LCB offset {lcb_off:.3f} m (trim at rest grows with it; limit 0.5 % L = {0.005 * h['length_wl_m']:.2f} m)")

    L, B, T = h["length_wl_m"], h["beam_wl_m"], h["draft_m"]
    cb = vol / (L * B * T)
    print(f"   L {L} m, B {B} m, T {T} m, displacement {disp} t, Cb {cb:.3f}")
    in_range("L/B", L / B)
    in_range("B/T", B / T)
    in_range("Cb", cb)
    k = h["gyradius_m"]
    in_range("k_roll/B", k["roll"] / B)
    in_range("GM/B", h["gm_m"] / B)

    t_undamped = 2 * math.pi * k["roll"] / math.sqrt(G * h["gm_m"])
    c = 0.373 + 0.023 * (B / T) - 0.043 * (L / 100.0)
    t_imo = 2 * c * B / math.sqrt(h["gm_m"])
    print(f"   roll period: undamped {t_undamped:.2f} s (2 pi k / sqrt(g GM), no added inertia); IMO 2008 IS Code {t_imo:.2f} s")
    say("PASS" if abs(t_undamped - t_imo) / t_imo < 0.15 else "WARN",
        "the two roll periods agree within 15 % (otherwise check the roll gyradius); hull-stability-check has the full theory")

    authored = []
    def walk(node, path):
        if isinstance(node, dict):
            for key, val in node.items():
                walk(val, f"{path}.{key}" if path else key)
        elif isinstance(node, str) and "AUTHORED" in node:
            authored.append(f"{path}: {node}")
    walk(lay, "")
    print(f"   AUTHORED values ({len(authored)}), report them:")
    for a in authored:
        print(f"     - {a}")
    print(f"== {'FAIL' if fails else 'OK'} ({fails} failing check{'s' if fails != 1 else ''})")
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()

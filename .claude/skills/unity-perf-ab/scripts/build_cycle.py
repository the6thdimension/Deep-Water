# Read one edit -> compile -> domain-reload cycle out of an editor_log_clock.py file, plus the
# per-assembly compiler (Csc) and IL post-processing (ILPostProcess) time from Bee's newest trace.
#
# usage:  python -I build_cycle.py <clock.txt> <label> <project>/Library/Bee
# t0 is the line "MARK <label> ..."; the cycle ends at the next MARK. Prints seconds after t0:
#   refresh       the Asset Pipeline Refresh that found the change (its own Total)
#   tundra        when the script build finished, its seconds and nodes updated
#   reload_begin / reload_end   domain reload start and "Mono: successfully reloaded" (= ready)
#   reload_ms     Unity's own "Domain Reload Profiling" figure
import glob, json, os, re, sys

def cycle(clock, label):
    rows = [l.rstrip("\n").split(" ", 1) for l in open(clock, encoding="utf-8") if " " in l]
    start = next(i for i, (_, s) in enumerate(rows) if s.startswith(f"MARK {label} ") or s == f"MARK {label}")
    t0 = float(rows[start][0])
    out = {}
    for t, s in rows[start + 1:]:
        t = round(float(t) - t0, 1)
        if s.startswith("MARK "):
            break
        if "Tundra build success" in s and "tundra" not in out:
            m = re.search(r"\(([\d.]+) seconds\), (\d+) items updated", s)
            out["tundra"] = (t, float(m.group(1)), int(m.group(2)))
        elif "Begin MonoManager ReloadAssembly" in s:
            out.setdefault("reload_begin", t)
        elif "Mono: successfully reloaded" in s:
            out.setdefault("reload_end", t)
        elif s.startswith("Domain Reload Profiling"):
            out.setdefault("reload_ms", int(re.search(r"(\d+)ms", s).group(1)))
        elif "Asset Pipeline Refresh" in s and "ForceDomainReload" not in s and "refresh" not in out:
            total = float(re.search(r"Total: ([\d.]+)", s).group(1))
            if total > 1:
                out["refresh"] = (t, total)
    return out

def bee_actions(bee_dir, top=14):
    trace = max(glob.glob(os.path.join(bee_dir, "backend*.traceevents")), key=os.path.getmtime)
    txt = open(trace, encoding="utf-8").read().strip()
    txt = ("[" + txt if not txt.startswith("[") else txt).rstrip(",\n ")
    if not txt.endswith("]"):
        txt += "]"
    acts = {}
    for e in json.loads(txt):
        if e.get("ph") == "X" and e.get("name") in ("Csc", "ILPostProcess"):
            key = (e["name"], (e.get("args") or {}).get("detail", "").split("/")[-1][:60])
            acts[key] = acts.get(key, 0) + e.get("dur", 0)
    return sorted(acts.items(), key=lambda x: -x[1])[:top]

if __name__ == "__main__":
    clock, label, bee = sys.argv[1:4]
    print(label, cycle(clock, label))
    for (name, detail), us in bee_actions(bee):
        print(f"   {us / 1e3:8.0f} ms  {name:14} {detail}")

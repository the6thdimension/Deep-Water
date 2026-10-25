# pkg_refs.py <project> <package-cache-dir>...: which Assets files reference any GUID a package ships.
import os, re, sys, collections
proj = sys.argv[1]
gre = re.compile(rb"guid: ([0-9a-f]{32})")
# index every text asset under Assets once: guid -> files
idx = collections.defaultdict(set)
for dp, dn, fn in os.walk(os.path.join(proj, "Assets")):
    for f in fn:
        if f.endswith((".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".playable", ".overrideController", ".mask", ".preset", ".asmref", ".asmdef", ".lighting", ".shadergraph", ".vfx", ".terrainlayer", ".spriteatlas", ".flare", ".renderTexture")):
            p = os.path.join(dp, f)
            try: data = open(p, "rb").read()
            except OSError: continue
            if not data.startswith(b"%YAML") and not f.endswith((".asmdef", ".asmref", ".shadergraph", ".vfx")): continue
            for g in set(gre.findall(data)): idx[g.decode()].add(os.path.relpath(p, proj))
for pkg in sys.argv[2:]:
    guids = {}
    for dp, dn, fn in os.walk(pkg):
        for f in fn:
            if f.endswith(".meta"):
                try:
                    for line in open(os.path.join(dp, f), encoding="utf-8", errors="ignore"):
                        if line.startswith("guid:"): guids[line.split()[1]] = os.path.relpath(os.path.join(dp, f[:-5]), pkg); break
                except OSError: pass
    hits = collections.defaultdict(set)
    for g, asset in guids.items():
        for user in idx.get(g, ()): hits[user].add(asset)
    print(f"== {os.path.basename(pkg)}: {len(guids)} GUIDs, referenced by {len(hits)} project files")
    for user, assets in sorted(hits.items())[:25]:
        print(f"   {user}  <-  {', '.join(sorted(assets)[:3])}{' …' if len(assets) > 3 else ''}")
